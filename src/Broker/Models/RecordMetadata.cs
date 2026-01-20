namespace Broker.Models;

public sealed record RecordMetadata(
    long Offset,
    long TimestampMs, // for v1, same as Record.TimestampMs
    long SegmentBaseOffset, // first offset of the segment file that contains this record
    long FilePosition, // byte offset within the .log file where this record’s frame begins
    int RecordSizeBytes // the total size in bytes of the record frame on disk
);