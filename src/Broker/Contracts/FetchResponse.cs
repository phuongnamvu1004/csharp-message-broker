using Broker.Storage;

namespace Broker.Contracts;

public sealed record FetchResponse(
    string Topic,
    int Partition,
    IReadOnlyList<LogRecord> Records,
    long NextOffset, // next offset to fetch from in the next poll
    long LogEndOffset
);