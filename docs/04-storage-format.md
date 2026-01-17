# Storage Format

**Purpose:** durability & recovery truth.

## Disk Layout

```aiignore
data/
  meta/
    broker.id
    checkpoint.json
  topics/
    <topic>/
      <partition>/
        segments/
          00000000000000000000.log
          00000000000000000000.idx
          00000000000000000000.timeidx   (optional)
          00000000000000000000.tomb      (optional)
```
**Naming rule:** segment base offset is a 20-digit zero-padded integer (lexicographic sort = numeric sort).

## What each file is

### `data/meta/broker.id`
A small text/binary file that uniquely identifies this broker instance across restarts.

**Used for:**
- Cluster membership and replication identity (later)
- Preventing accidental reuse of the same data directory by two different broker identities

**Recommended contents (v1):**
- `uuid` (e.g., a GUID)
- `created_at` timestamp
- optional: `node_name`

**Write rules:**
- Create once on first boot if missing
- Never modify in place (only rewrite atomically if you ever change format)

---

### `data/meta/checkpoint.json`
A durability and fast-startup snapshot of what the broker believes is safely on disk.

**Used for:**
- Avoid scanning every segment at startup
- Knowing the next offset to assign (`log_end_offset`)
- Knowing which segment is active / last flushed offset

**Minimum fields per partition (v1):**
- `topic`
- `partition`
- `log_end_offset` (next offset to assign)
- `active_segment_base_offset`
- `active_segment_size_bytes`
- optional: `last_flushed_offset` (if you separate buffered vs flushed)

**Write rules (important):**
- Write to `checkpoint.tmp` then `fsync`
- Rename to `checkpoint.json`
- `fsync` the directory

**Crash behavior:**
- If checkpoint is missing or stale, the broker must rebuild truth by scanning segment(s) and truncating to last valid frame.

---

### `data/topics/<topic>/<partition>/segments/<base>.log`
The append-only segment log file containing the actual record bytes for this partition.

**Used for:**
- Storing records in order by offset
- Sequential reads for consumers
- Recovery: scan frames, validate checksum, truncate partial tail

**Key properties:**
- Append-only; never overwrite in place
- Contains framed records (header + body + checksum + length)
- `base` in the filename is the first offset in the segment

**Lifecycle:**
- One segment is “active” (currently appended)
- When it reaches `segment_max_bytes` (or time-based roll), it becomes “closed” and a new active segment is created

---

### `data/topics/<topic>/<partition>/segments/<base>.idx`
A sparse offset→file-position index for the corresponding `.log` segment.

**Used for:**
- Fast seek: binary search by offset, jump into the `.log`, then scan forward

**Key properties:**
- Usually sparse (e.g., every 100 records)
- Entries are monotonic by offset
- Can be rebuilt by scanning the `.log` if missing/corrupt

---

### `data/topics/<topic>/<partition>/segments/<base>.timeidx` (optional)
A time→offset (or time→file-position) index for time-based fetch (e.g., “start from timestamp”).

**Used for:**
- `FetchFromTimestamp(ts)` APIs
- Faster time-based retention bookkeeping (optional)

**Key properties:**
- Sparse mapping (e.g., every N records or every M milliseconds)
- Rebuildable from `.log` by reading record timestamps

---

### `data/topics/<topic>/<partition>/segments/<base>.tomb` (optional)
A tombstone marker file that indicates a segment is being deleted or was scheduled for deletion.

**Used for:**
- Safer retention deletion across crashes

**Deletion flow (recommended):**
1. Create `<base>.tomb`
2. Delete `<base>.idx` / `<base>.timeidx` (if any)
3. Delete `<base>.log`
4. Delete `<base>.tomb`

**Crash behavior:**
- If the broker restarts and finds a `.tomb`, it can retry/finish deletion and avoid serving partially-deleted segments.


## File write rules

### First boot (empty data/)

When starting the broker the very first time:
- Create data/ and subfolders as needed.
- Create data/meta/broker.id (one-time identity).
- Create an initial checkpoint.json (can be empty or “no topics yet”).

Nothing else exists until we actually create a topic/partition or receive data.

### Later boots (existing data/)

Startup does recovery + resume:
- Read broker.id (must exist).
- Load checkpoint.json (if missing/stale, rebuild by scanning segments).
- For each partition folder found:
  - open segments
  - recover active segment (scan/truncate partial tail if needed)
  - rebuild .idx/.timeidx if needed
  - Then the broker is ready to accept requests.

### When the broker receives requests
**On Publish (produce)**

A publish turns into:
1.	Ensure the topic/partition directories exist: `data/topics/<topic>/<partition>/segments/`
2.	Ensure there is an active segment:
   - if none exists → create `00000000000000000000.log` (+ `.idx`)
   - if active segment too large → roll to a new `<base>.log`
3.	Append a framed record to the active `.log`
4.	Append an index entry to `.idx` (sparse or every record)
5.	Update in-memory state (`log_end_offset`, `segment size`)
6.	Periodically (or on flush policy) write `checkpoint.json` atomically

**On Fetch (consume)**

A subscribe request typically does not write to the log. It mostly:
- finds the right segment by offset
- uses .idx to jump into .log
- streams records out

The big question is whether your broker tracks consumer progress.

**Option A (simpler v1): “stateless consumers”**
- subscriber supplies fromOffset
- broker returns records from that offset
- broker does not store consumer offsets

✅ no extra files needed

**Option B: “tracked consumer offsets” (more Kafka-like)**
- broker stores committed offsets per (group, topic, partition)
- that adds another storage area, e.g.
- `data/meta/consumer-offsets/...` or a special internal topic

✅ more features, more files
