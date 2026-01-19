namespace Broker.Storage;

// sealed to prevent inheritance, records are kept as bytes for consistency
public sealed record LogRecord(
    byte[]? Key,
    byte[] Value,
    long TimestampMs // more like CreateTime
);