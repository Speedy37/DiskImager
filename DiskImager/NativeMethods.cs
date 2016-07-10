using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;

namespace DiskImager
{
    class NativeMethods
    {
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
            IntPtr lpInBuffer, uint nInBufferSize,
            IntPtr lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);

        // dwDesiredAccess
        public const uint GENERIC_READ      = 0x80000000;
        public const uint GENERIC_WRITE     = 0x40000000;
        public const uint GENERIC_EXECUTE   = 0x20000000;

        // dwShareMode
        public const uint FILE_SHARE_DELETE = 0x00000004;
        public const uint FILE_SHARE_WRITE  = 0x00000002;
        public const uint FILE_SHARE_READ   = 0x00000001;

        // dwCreationDisposition
        public const uint CREATE_NEW = 1;
        public const uint CREATE_ALWAYS = 2;
        public const uint OPEN_EXISTING = 3;
        public const uint OPEN_ALWAYS = 4;
        public const uint TRUNCATE_EXISTING = 5;        

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static public extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode, 
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes, 
            IntPtr hTemplateFile
            );

        // dwIoControlCode
        public const uint FSCTL_IS_VOLUME_MOUNTED = 0x00090028;
        public const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
        public const uint FSCTL_LOCK_VOLUME = 0x00090018;
        public const uint FSCTL_UNLOCK_VOLUME = 0x0009001c;

        [DllImport("Kernel32.dll", SetLastError = true)]
        static public extern bool DeviceIoControl(
           SafeFileHandle hDevice,
           uint dwIoControlCode,
           [In] IntPtr lpInBuffer,
           uint nInBufferSize,
           [Out] IntPtr lpOutBuffer,
           uint nOutBufferSize,
           ref uint lpBytesReturned,
           [In] IntPtr Overlapped
           );

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

    }
}
