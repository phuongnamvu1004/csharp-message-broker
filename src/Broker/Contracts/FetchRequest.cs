namespace Broker.Contracts;

public sealed record FetchRequest(
    string Topic,
    int Partition,
    long FromOffset,
    int MaxBytes,
    int MaxRecords,
    int? WaitMs
);