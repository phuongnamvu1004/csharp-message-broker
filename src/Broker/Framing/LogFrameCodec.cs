using Broker.Models;
using System.Buffers.Binary;
using System.IO.Hashing;

namespace Broker.Framing;

public sealed class LogFrameCodec
{
    private static RecordMetadata WriteFrame(FileStream fs, long nextOffset, LogRecord logRecord)
    {
        // Sanity checks
        ArgumentNullException.ThrowIfNull(fs);
        if (!fs.CanWrite)
        {
            throw new ArgumentException("FileStream must be writable.", nameof(fs));
        }
        ArgumentNullException.ThrowIfNull(logRecord);
        if (logRecord.Value is null)
        {
            throw new ArgumentException("Record.Value must not be null.", nameof(logRecord));
        }

        // Extract fields
        var key = logRecord.Key;
        var value = logRecord.Value;

        var keyLen = (uint)(key?.Length ?? 0);
        var valueLen = (uint)value.Length;

        // Basic sanity guards (tune later via config)
        if (keyLen + valueLen > LogFrameConstants.MaxPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(logRecord), $"Key+Value exceeds max payload {LogFrameConstants.MaxPayloadBytes} bytes.");
        }

        // If TimestampMs is 0, treat it as "broker sets append time" (v1 convenience)
        var ts = logRecord.TimestampMs != 0
            ? logRecord.TimestampMs
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var bodyLen = checked((int)keyLen + (int)valueLen);
        var frameLen = checked(LogFrameConstants.HeaderSizeBytes + bodyLen + LogFrameConstants.TrailerSizeBytes);

        // Build Header
        Span<byte> header = stackalloc byte[LogFrameConstants.HeaderSizeBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(header[..4], LogFrameConstants.Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(4, 2), LogFrameConstants.Version);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(6, 2), 0); // flags (v1: 0)
        BinaryPrimitives.WriteInt64LittleEndian(header.Slice(8, 8), nextOffset);
        BinaryPrimitives.WriteInt64LittleEndian(header.Slice(16, 8), ts);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(24, 4), keyLen);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(28, 4), valueLen);

        // Compute CRC32 over header+body
        var crc32 = new Crc32();
        crc32.Append(header);
        if (key is not null) // same as keyLen > 0
        {
            crc32.Append(key);
        }
        if (valueLen > 0)
        {
            crc32.Append(value);
        }
        var crc = BinaryPrimitives.ReadUInt32LittleEndian(crc32.GetCurrentHash()); // CRC32 checksum for header+body

        // Write frame to stream: header, body, trailer
        var frameStartPos = fs.Position;
        // header
        fs.Write(header);
        // body
        if (keyLen > 0)
        {
            fs.Write(key!, 0, key!.Length);
        }
        if (valueLen > 0)
        {
            fs.Write(value, 0, value.Length);
        }
        // trailer
        Span<byte> trailer = stackalloc byte[LogFrameConstants.TrailerSizeBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(trailer[..4], crc);
        BinaryPrimitives.WriteUInt32LittleEndian(trailer.Slice(4, 4), (uint)frameLen);
        fs.Write(trailer);

        // NOTE: flushing/fsync is controlled at a higher level (segment/partition/broker flush policy)

        // SegmentBaseOffset is not known at this layer; set to -1 for now.
        return new RecordMetadata(
            Offset: nextOffset,
            TimestampMs: ts,
            SegmentBaseOffset: -1,
            FilePosition: frameStartPos,
            RecordSizeBytes: frameLen
        );
    }

    private static bool TryReadExactly(FileStream fs, Span<byte> buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = fs.Read(buffer.Slice(totalRead));
            if (read <= 0)
            {
                return false;
            }
            totalRead += read;
        }
        return true;
    }

    private static bool TryReadExactly(FileStream fs, byte[] buffer)
        => TryReadExactly(fs, buffer.AsSpan());

    private static IEnumerable<LogRecord> ReadFrame(FileStream fs, long fromOffset, int maxBytes, int maxRecords)
    {
        // Sanity checks
        ArgumentNullException.ThrowIfNull(fs);
        if (!fs.CanRead)
        {
            throw new ArgumentException("FileStream must be readable.", nameof(fs));
        }
        if (maxBytes <= 0) yield break;
        if (maxRecords <= 0) yield break;

        // Start scanning from the beginning for v1 (index-based seeking will come later).
        fs.Position = 0;

        var bytesReturned = 0;
        var recordsReturned = 0;

        var header = new byte[LogFrameConstants.HeaderSizeBytes];
        var trailer = new byte[LogFrameConstants.TrailerSizeBytes];

        while (recordsReturned < maxRecords && bytesReturned < maxBytes)
        {
            var frameStart = fs.Position;

            // Try to read the header. If we can't read a full header, it's a partial tail.
            if (!TryReadExactly(fs, header))
            {
                yield break;
            }

            // Parse and validate header
            var magic = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4));
            if (magic != LogFrameConstants.Magic)
            {
                // Not a valid frame boundary; treat as corruption/tail and stop.
                yield break;
            }

            var version = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4, 2));
            if (version != LogFrameConstants.Version)
            {
                // Unknown format for v1 reader
                yield break;
            }

            // flags are currently unused (v1)
            var offset = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8, 8));
            var ts = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(16, 8));
            var keyLen = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24, 4));
            var valueLen = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(28, 4));

            // Size sanity
            if (keyLen + valueLen > LogFrameConstants.MaxPayloadBytes)
            {
                yield break;
            }

            var bodyLen = checked((int)keyLen + (int)valueLen);

            // Read body
            byte[]? key = null;
            if (keyLen > 0)
            {
                key = new byte[keyLen];
                if (!TryReadExactly(fs, key))
                {
                    yield break; // partial tail
                }
            }

            var value = Array.Empty<byte>();
            if (valueLen > 0)
            {
                value = new byte[valueLen];
                if (!TryReadExactly(fs, value))
                {
                    yield break; // partial tail
                }
            }

            // Read trailer
            if (!TryReadExactly(fs, trailer))
            {
                yield break; // partial tail
            }

            var storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(trailer.AsSpan(0, 4));
            var storedFrameLen = BinaryPrimitives.ReadUInt32LittleEndian(trailer.AsSpan(4, 4));

            var expectedFrameLen = checked(LogFrameConstants.HeaderSizeBytes + bodyLen + LogFrameConstants.TrailerSizeBytes);
            if (storedFrameLen != (uint)expectedFrameLen)
            {
                // Frame length mismatch => corruption
                yield break;
            }

            // Verify CRC over header + body
            var crc32 = new Crc32();
            crc32.Append(header);
            if (keyLen > 0)
            {
                crc32.Append(key!);
            }
            if (valueLen > 0)
            {
                crc32.Append(value);
            }

            var computedCrc = BinaryPrimitives.ReadUInt32LittleEndian(crc32.GetCurrentHash());

            if (computedCrc != storedCrc)
            {
                // Corruption detected
                yield break;
            }

            // Skip until we reach the requested offset
            if (offset < fromOffset)
            {
                continue;
            }

            // Enforce maxBytes based on on-disk frame size
            if (bytesReturned + expectedFrameLen > maxBytes)
            {
                // Rewind to frame start so the next fetch can resume cleanly if the caller wants.
                fs.Position = frameStart;
                yield break;
            }

            bytesReturned += expectedFrameLen;
            recordsReturned++;

            yield return new LogRecord(
                Key: key,
                Value: value,
                TimestampMs: ts
            );
        }
    }
    public static RecordMetadata LogFrameWriter(FileStream fs, long nextOffset, LogRecord logRecord)
        => WriteFrame(fs, nextOffset, logRecord);
    
    public static IEnumerable<LogRecord> LogFrameReader(FileStream fs, long fromOffset, int maxBytes, int maxRecords)
        => ReadFrame(fs, fromOffset, maxBytes, maxRecords);
}