namespace Broker.Storage;

public class LogFrameCodec
{
    /*
     * This method writes the record to the file stream
     * 1. Validate sizes (key/value length, max message size, etc.)
     * 2. Build the header fields (magic/version/flags/offset/timestamp/keyLen/valueLen)
     * 3. Write header → write key bytes → write value bytes
     * 4. Compute checksum over (header+body) and write checksum
     * 5. Write frame_len (so recovery can stop at partial tail)
     * 6. Return RecordMetadata:
     * - Offset (what caller passed)
     * - FilePosition (where the frame started)
     * - RecordSizeBytes (frame_len)
     * - TimestampMs
     */
    public void LogFrameWriter(FileStream fs, long nextOffset, Record record)
    {
        throw new NotImplementedException();
    }
    
    public IEnumerable<Record> LogFrameReader(FileStream fs, long fromOffset, int maxBytes, int maxRecords)
    {
        throw new NotImplementedException();
    }
}