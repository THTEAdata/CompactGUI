namespace CompactGUI.Core;


public sealed class AnalysedFileDetails
{
    public required string FileName { get; set; }
    public long UncompressedSize { get; set; }
    public long CompressedSize { get; set; }
    public WOFCompressionAlgorithm CompressionMode { get; set; }
    public FileInfo? FileInfo { get; set; }
}


public sealed class ExtensionResult
{
    public required string Extension { get; set; }
    public long UncompressedBytes { get; set; }
    public long CompressedBytes { get; set; }
    public int TotalFiles { get; set; }
    public double CRatio => CompressedBytes == 0 ? 0 : Math.Round((double)CompressedBytes / UncompressedBytes, 2);

}




public struct CompressionProgress
{
    public int ProgressPercent;
    public string FileName;

    public CompressionProgress(int progressPercent, string fileName)
    {
        ProgressPercent = progressPercent;
        FileName = fileName;
    }
}

// ==== 新增：信息显示类数据模型 ====

/// <summary>文件类型统计（按扩展名分组）</summary>
public sealed class FileTypeStat
{
    public required string Extension { get; set; }
    public int FileCount { get; set; }
    public long UncompressedBytes { get; set; }
    public long CompressedBytes { get; set; }
    public double CompressRatio => CompressedBytes == 0 ? 0 : Math.Round((double)CompressedBytes / UncompressedBytes, 4);
    public double SpaceSavingPercent => UncompressedBytes == 0 ? 0 : Math.Round((1 - (double)CompressedBytes / UncompressedBytes) * 100, 2);
    public string UncompressedSizeFormatted => FormatBytes(UncompressedBytes);
    public string CompressedSizeFormatted => FormatBytes(CompressedBytes);
    public string SavedFormatted => FormatBytes(UncompressedBytes - CompressedBytes);

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double d = bytes;
        while (d >= 1024 && i < suffixes.Length - 1) { d /= 1024; i++; }
        return $"{d:0.##} {suffixes[i]}";
    }
}

/// <summary>压缩过程实时仪表盘信息</summary>
public sealed class RealTimeProgressInfo
{
    public long TotalBytes { get; set; }
    public long ProcessedBytes { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public double ProgressPercent => TotalBytes == 0 ? 0 : Math.Round((double)ProcessedBytes / TotalBytes * 100, 1);
    public string CurrentFile { get; set; } = "";
    public double SpeedBytesPerSec { get; set; }
    public string SpeedFormatted => SpeedBytesPerSec switch
    {
        >= 1073741824 => $"{SpeedBytesPerSec / 1073741824:0.0} GB/s",
        >= 1048576 => $"{SpeedBytesPerSec / 1048576:0.0} MB/s",
        >= 1024 => $"{SpeedBytesPerSec / 1024:0.0} KB/s",
        _ => $"{SpeedBytesPerSec:0} B/s"
    };
    public TimeSpan Elapsed { get; set; }
    public string EtaFormatted
    {
        get
        {
            if (SpeedBytesPerSec <= 0) return "---";
            long remaining = TotalBytes - ProcessedBytes;
            double secs = remaining / SpeedBytesPerSec;
            if (secs < 60) return $"{(int)secs}s";
            if (secs < 3600) return $"{(int)secs / 60}m {(int)secs % 60}s";
            return $"{(int)secs / 3600}h {(int)(secs % 3600) / 60}m";
        }
    }

    // ==== 新增：系统性能监控 ====
    /// <summary>CompactGUI 进程 CPU 使用率 (%)</summary>
    public double CpuPercent { get; set; }
    public string CpuPercentFormatted => $"{CpuPercent:F1}%";

    /// <summary>CompactGUI 进程内存占用 (MB)</summary>
    public double MemoryMB { get; set; }
    public string MemoryFormatted => $"{MemoryMB:F0} MB";

    /// <summary>磁盘读取速率 (MB/s, 仅当前进程)</summary>
    public double DiskReadMBs { get; set; }
    public string DiskReadFormatted => $"{DiskReadMBs:F1} MB/s";

    /// <summary>磁盘写入速率 (MB/s, 仅当前进程)</summary>
    public double DiskWriteMBs { get; set; }
    public string DiskWriteFormatted => $"{DiskWriteMBs:F1} MB/s";
}

/// <summary>压缩历史记录条目</summary>
public sealed class CompressionHistoryEntry
{
    public required string FolderPath { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public long UncompressedBytes { get; set; }
    public long CompressedBytes { get; set; }
    public int TotalFiles { get; set; }
    public string CompressionMode { get; set; } = "";
    public double SpaceSavingPercent => UncompressedBytes == 0 ? 0 : Math.Round((1 - (double)CompressedBytes / UncompressedBytes) * 100, 2);
    public TimeSpan Duration { get; set; }
}


public enum CompressionMode: int
{
    XPRESS4K,
    XPRESS8K,
    XPRESS16K,
    LZX,
    None
}


public enum WOFCompressionAlgorithm: int
{
    NO_COMPRESSION = -2,
    LZNT1 = -1,
    XPRESS4K = 0,
    LZX = 1,
    XPRESS8K = 2,
    XPRESS16K = 3
}