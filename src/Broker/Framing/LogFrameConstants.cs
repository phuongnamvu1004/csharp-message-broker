namespace Broker.Framing;

public static class LogFrameConstants
{
    public const uint Magic = 0xB10B5E01;
    public const ushort Version = 1;
    
    public const int MaxPayloadBytes = 16 * 1024 * 1024; // 16MB v1 default
    
    public const int HeaderSizeBytes = 32;
    public const int TrailerSizeBytes = 8; // u32 crc32 + u32 frameLen
}