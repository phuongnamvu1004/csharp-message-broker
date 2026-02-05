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

#### Log record frame format (v1)

Each `.log` segment is a concatenation of **frames**. One frame stores exactly one record.

A frame layout is:

```
[ HEADER (32 bytes) ][ KEY (key_len bytes) ][ VALUE (value_len bytes) ][ CRC32 (4 bytes) ][ FRAME_LEN (4 bytes) ]
```

All integer fields are **little-endian**.

##### Header (32 bytes)

| Offset | Size | Field          | Type  | Meaning                                                                   |
|-------:|-----:|----------------|-------|---------------------------------------------------------------------------|
|      0 |    4 | `magic`        | `u32` | Constant marker for frame start (v1: `0xB10B5E01`)                        |
|      4 |    2 | `version`      | `u16` | Frame format version (v1: `1`)                                            |
|      6 |    2 | `flags`        | `u16` | Reserved bit flags (v1: `0`)                                              |
|      8 |    8 | `offset`       | `i64` | Record offset within the partition (assigned by broker)                   |
|     16 |    8 | `timestamp_ms` | `i64` | Record timestamp in milliseconds since epoch (v1 uses broker append time) |
|     24 |    4 | `key_len`      | `u32` | Length of key in bytes (0 allowed)                                        |
|     28 |    4 | `value_len`    | `u32` | Length of value in bytes                                                  |

##### Body (variable)

- `key` bytes: `key_len` bytes (may be absent if `key_len = 0`)
- `value` bytes: `value_len` bytes

##### Trailer (8 bytes)

| Order | Size | Field       | Type  | Meaning                                                         |
|------:|-----:|-------------|-------|-----------------------------------------------------------------|
|     1 |    4 | `crc32`     | `u32` | CRC32 over **header + body** (does **not** include the trailer) |
|     2 |    4 | `frame_len` | `u32` | Total frame size in bytes: `32 + key_len + value_len + 8`       |

##### Recovery rules

- On startup, the broker scans the active segment frame-by-frame.
- Scanning stops at the first invalid frame (partial tail, bad magic/version, size overflow, or CRC mismatch).
- The segment is truncated back to the last valid frame boundary (“truncate-to-last-valid”).

