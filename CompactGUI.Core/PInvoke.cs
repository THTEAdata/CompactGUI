// 替代 Microsoft.Windows.CsWin32 NuGet 包（Fallback）
// 当 CsWin32 包未还原时使用手工 P/Invoke 声明
// 若 CsWin32 可用（定义了 CSWIN32_GENERATED），整个文件被条件编译跳过

#if !CSWIN32_GENERATED

using System.Runtime.InteropServices;

namespace Windows.Win32
{
    internal static class PInvoke
    {
        // ===== WOF (Windows Overlay Filter) API (wofapi.dll) =====
        [DllImport("wofapi.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern unsafe int WofSetFileDataLocation(
            SafeHandle FileHandle,
            uint Algorithm,
            void* CompressionInfo,
            uint CompressionInfoSize);

        [DllImport("wofapi.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern unsafe uint WofIsExternalFile(
            [MarshalAs(UnmanagedType.LPWStr)] string FileName,
            Foundation.BOOL* IsExternalFile,
            uint* Provider,
            void* CompressionInfo,
            uint* BufferSize);

        // ===== Kernel32 API =====
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto)]
        internal static extern uint GetShortPathName(
            [MarshalAs(UnmanagedType.LPTStr)] string lpszLongPath,
            Span<char> lpszShortPath,
            uint cchBuffer);

        // Overload without cchBuffer for Span<T> call
        internal static unsafe uint GetShortPathName(string lpszLongPath, Span<char> lpszShortPath)
        {
            return GetShortPathName(lpszLongPath, lpszShortPath, (uint)lpszShortPath.Length);
        }

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern unsafe bool GetDiskFreeSpace(
            [MarshalAs(UnmanagedType.LPTStr)] string lpRootPathName,
            uint* lpSectorsPerCluster,
            uint* lpBytesPerSector,
            uint* lpNumberOfFreeClusters,
            uint* lpTotalNumberOfClusters);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto)]
        internal static extern uint GetCompressedFileSize(
            [MarshalAs(UnmanagedType.LPTStr)] string lpFileName,
            uint* lpFileSizeHigh);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern unsafe bool DeviceIoControl(
            SafeHandle hDevice,
            uint dwIoControlCode,
            void* lpInBuffer,
            uint nInBufferSize,
            void* lpOutBuffer,
            uint nOutBufferSize,
            uint* lpBytesReturned,
            void* lpOverlapped);
    }

    // ===== 补充类型 =====
    [Flags]
    internal enum EXECUTION_STATE : uint
    {
        ES_CONTINUOUS = 0x80000000,
        ES_SYSTEM_REQUIRED = 0x00000001,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_USER_PRESENT = 0x00000004,
        ES_AWAYMODE_REQUIRED = 0x00000040,
    }

    namespace Foundation
    {
        /// <summary>
        /// 兼容 CsWin32 的 BOOL 类型（4字节，对应 Win32 BOOL）
        /// </summary>
        internal readonly struct BOOL : IEquatable<BOOL>
        {
            private readonly int _value;

            private BOOL(int value) => _value = value;

            public static readonly BOOL FALSE = new BOOL(0);
            public static readonly BOOL TRUE = new BOOL(1);

            public static implicit operator bool(BOOL value) => value._value != 0;
            public static implicit operator BOOL(bool value) => new BOOL(value ? 1 : 0);

            public static bool operator true(BOOL value) => value._value != 0;
            public static bool operator false(BOOL value) => value._value == 0;

            public static BOOL operator !(BOOL value) => new BOOL(value._value == 0 ? 1 : 0);

            public bool Equals(BOOL other) => _value == other._value;
            public override bool Equals(object? obj) => obj is BOOL other && Equals(other);
            public override int GetHashCode() => _value;
            public override string ToString() => _value != 0 ? "TRUE" : "FALSE";

            public static bool operator ==(BOOL left, BOOL right) => left._value == right._value;
            public static bool operator !=(BOOL left, BOOL right) => left._value != right._value;
        }
    }

    namespace System.Power
    {
        // 保留命名空间占位，EXECUTION_STATE 实际定义在 Windows.Win32 命名空间
    }
}

#endif
