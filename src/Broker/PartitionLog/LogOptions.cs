namespace Broker.PartitionLog;

public sealed class LogOptions
{
    public required string DirectoryPath { get; init; }  // e.g. data/topic/partition

    public long SegmentMaxBytes { get; init; } = 64 * 1024 * 1024; // dev: smaller
    public bool EnableIndex { get; init; } = true;

    // Durability policy (MVP can ignore or implement lightly)
    public int FlushEveryNAppends { get; init; } = 0; // FlushEveryNAppends == 0 → never force flush
                                                      // FlushEveryNAppends == 1 → flush every append
                                                      // FlushEveryNAppends == N → flush every N appends
}