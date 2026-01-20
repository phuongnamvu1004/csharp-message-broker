using Broker.Models;

namespace Broker.PartitionLog;

public class PartitionLog : IPartitionLog
{
    private readonly LogOptions _options;
    private readonly object _gate = new();
    private FileStream _activeStream;
    private long _nextOffset;
    private bool _disposed;
    
    public PartitionLog(LogOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        
        ValidateOptions(_options); // validate options
        
        Directory.CreateDirectory(_options.DirectoryPath); // create a directory if it doesn't exist
        
        var activePath = GetActiveLogFilePath(_options.DirectoryPath); // open/create an active log file
        _activeStream = OpenAppendStream(activePath);
        
        _nextOffset = RecoverNextOffset(_activeStream); // recover the next offset (MVP: scan file; later: checkpoint/index)
    }
    
    public RecordMetadata Append(LogRecord record)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));
        ThrowIfDisposed();

        lock (_gate)
        {
            // TODO (Step 1): offset = _nextOffset
            // TODO (Step 2): write frame via LogFrameCodec.WriteFrame(_activeStream, offset, record)
            // TODO (Step 3): advance _nextOffset
            // TODO (Step 4): optionally flush depending on options
            throw new NotImplementedException();
        }
    }

    public LogRecord Read(long offset)
    {
        throw new NotImplementedException();
    }

    public long GetLogEndOffset()
    {
        throw new NotImplementedException();
    }
    
    private static void ValidateOptions(LogOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            throw new ArgumentException("DirectoryPath must be set.", nameof(options));

        // Optional: validate thresholds etc.
        // if (options.SegmentMaxBytes <= 0) ...
        throw new NotImplementedException();
    }
    
    private static string GetActiveLogFilePath(string directoryPath)
    {
        // MVP: single file name. Later: use baseOffset-padded names for segments.
        // return Path.Combine(directoryPath, "active.log");
        throw new NotImplementedException();
    }

    private static FileStream OpenAppendStream(string path)
    {
        throw new NotImplementedException();
    }
    
    private static long RecoverNextOffset(FileStream stream)
    {
        // TODO (MVP): scan frames to the end and compute lastOffset+1.
        // For now, return 0 so you can implement recovery as the next step.
        // IMPORTANT: leave stream positioned at end for appends.
        return 0;
    }
    
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PartitionLog));
    }
}