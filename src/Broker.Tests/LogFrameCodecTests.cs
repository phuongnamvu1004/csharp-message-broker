using Broker.Framing;
using Broker.Models;

namespace Broker.Tests;

public sealed class LogFrameCodecTests
{
    [Fact]
    public void WriteThenRead_RoundTripsRecords()
    {
        var path = Path.GetTempFileName();
        try
        {
            // Write
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                LogFrameCodec.LogFrameWriter(fs, 0, new LogRecord(null, [1, 2, 3], 1700000000000));
                LogFrameCodec.LogFrameWriter(fs, 1, new LogRecord([9], [4, 5], 1700000000001));
                LogFrameCodec.LogFrameWriter(fs, 2, new LogRecord(null, [], 1700000000002));
                fs.Flush(true);
            }

            // Read
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var records = LogFrameCodec.LogFrameReader(fs, fromOffset: 0, maxBytes: 1024 * 1024, maxRecords: 100)
                    .ToList();

                Assert.Equal(3, records.Count);

                Assert.Null(records[0].Key);
                Assert.Equal([1, 2, 3], records[0].Value);
                Assert.Equal(1700000000000, records[0].TimestampMs);

                Assert.Equal([9], records[1].Key);
                Assert.Equal([4, 5], records[1].Value);
                Assert.Equal(1700000000001, records[1].TimestampMs);

                Assert.Null(records[2].Key);
                Assert.Empty(records[2].Value);
                Assert.Equal(1700000000002, records[2].TimestampMs);
            }
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    [Fact]
    public void Read_FromOffsetSkipsEarlierRecords()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                LogFrameCodec.LogFrameWriter(fs, 0, new LogRecord(null, [1], 1700000000000));
                LogFrameCodec.LogFrameWriter(fs, 1, new LogRecord(null, [2], 1700000000001));
                LogFrameCodec.LogFrameWriter(fs, 2, new LogRecord(null, [3], 1700000000002));
                fs.Flush(true);
            }

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var records = LogFrameCodec.LogFrameReader(fs, fromOffset: 2, maxBytes: 1024 * 1024, maxRecords: 100)
                    .ToList();
                Assert.Single(records);
                Assert.Equal([3], records[0].Value);
            }
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    [Fact]
    public void Read_PartialTailStopsCleanly()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                LogFrameCodec.LogFrameWriter(fs, 0, new LogRecord(null, [10], 1700000000000));
                LogFrameCodec.LogFrameWriter(fs, 1, new LogRecord(null, [11], 1700000000001));
                LogFrameCodec.LogFrameWriter(fs, 2, new LogRecord(null, [12], 1700000000002));
                fs.Flush(true);
            }

            // Simulate a crash: truncate mid-frame (remove last 5 bytes)
            var len = new FileInfo(path).Length;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                fs.SetLength(Math.Max(0, len - 5));
                fs.Flush(true);
            }

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var records = LogFrameCodec.LogFrameReader(fs, fromOffset: 0, maxBytes: 1024 * 1024, maxRecords: 100)
                    .ToList();

                // Should return the fully written frames only.
                Assert.Equal(2, records.Count);
                Assert.Equal([10], records[0].Value);
                Assert.Equal([11], records[1].Value);
            }
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    [Fact]
    public void Read_RespectsMaxRecords()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                for (long i = 0; i < 10; i++)
                {
                    LogFrameCodec.LogFrameWriter(fs, i, new LogRecord(null, [(byte)i], 1700000000000 + i));
                }

                fs.Flush(true);
            }

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var records = LogFrameCodec.LogFrameReader(fs, fromOffset: 0, maxBytes: 1024 * 1024, maxRecords: 3)
                    .ToList();
                Assert.Equal(3, records.Count);
            }
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                /* ignore */
            }
        }
    }
}