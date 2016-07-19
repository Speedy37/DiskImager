using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DiskImager
{
    public class DiskPartition
    {
        public DiskPartition(ManagementBaseObject obj)
        {
            Index = (uint)obj["Index"];
            DeviceID = (string)obj["DeviceID"];
            StartingOffset = (ulong)obj["StartingOffset"];
            Size = (ulong)obj["Size"];
            PrimaryPartition = (bool)obj["PrimaryPartition"];
            BlockSize = (ulong)obj["BlockSize"];
            MountPoints = new List<string>();
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + DeviceID + "'} WHERE AssocClass = Win32_LogicalDiskToPartition");
            foreach (var sobj in searcher.Get())
            {
                VolumeName = (string)sobj["VolumeName"];
                MountPoints.Add((string)sobj["Name"]);
                FreeSpace = (ulong)sobj["FreeSpace"];
                FileSystem = DiskDrive.CastOrDefault(sobj, "FileSystem", "Unknown");
            }
        }

        public string DeviceID { get; private set; }
        public uint Index { get; private set; }
        public ulong StartingOffset { get; private set; }
        public ulong Size { get; private set; }
        public bool PrimaryPartition { get; private set; }
        public ulong BlockSize { get; private set; }
        public string VolumeName { get; private set; }
        public List<string> MountPoints { get; private set; }
        public string MountPoint { get { return String.Join(", ", MountPoints); } }
        public ulong FreeSpace { get; private set; }
        public string FileSystem { get; private set; }
        
        private SafeFileHandle handle;
        internal void LockAndDismount()
        {
            if (handle != null)
                throw new InvalidOperationException("An handle is already open");
            string letter = MountPoints.Find(m => true);
            if (letter == null)
                return;
            string path = "\\\\.\\" + letter;
            handle = NativeMethods.CreateFile(path, NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE, NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle = null;
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
            uint junk = 0;
            if (!NativeMethods.DeviceIoControl(handle, NativeMethods.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref junk, IntPtr.Zero)
             || !NativeMethods.DeviceIoControl(handle, NativeMethods.FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref junk, IntPtr.Zero))
            {
                handle.Dispose();
                handle = null;
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
        }

        internal void UnLock()
        {
            if (handle != null)
            {
                uint junk = 0;
                NativeMethods.DeviceIoControl(handle, NativeMethods.FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref junk, IntPtr.Zero);
                handle.Dispose();
                handle = null;
            }
        }
    }
}

/*Console.WriteLine("------DiskPartition------------");
foreach (var p in obj.Properties)
{
    if (p.Value != null)
        Console.WriteLine("{0} = ({2})obj[\"{0}\"]; // {1}", p.Name, p.Value, p.Value != null ? p.Value.GetType() : null);
}
Console.WriteLine("-----------------------------------");
foreach (var p in obj.Properties)
{
    if (p.Value != null)
        Console.WriteLine("readonly {2} {0};", p.Name, p.Value, p.Value != null ? p.Value.GetType() : null);
}
Console.WriteLine("-----------------------------------");*/


