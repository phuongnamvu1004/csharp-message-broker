using Broker.Models;

namespace Broker.PartitionLog;

public interface IPartitionLog
{
    RecordMetadata Append(LogRecord record);
    LogRecord Read(long offset);
    long GetLogEndOffset(); // optional 
}