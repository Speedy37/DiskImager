using System;
using System.Collections.Generic;
using System.Management;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace DiskImager
{
    public class DiskDrive : ISource
    {
        static internal T CastOrDefault<T>(ManagementBaseObject obj, string key, T defaultValue)
        {
            try
            {
                var value = obj[key];
                return value != null ? (T)value : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public delegate void ChangedEventHandler();
        public class SharedDrives
        {
            private List<DiskDrive> drives = null;
            private ManagementEventWatcher w = null;

            public event ChangedEventHandler Changed;

            public void ListenForDriveChanges()
            {
                if (w == null)
                {
                    ManagementScope scope = new ManagementScope("root\\CIMV2");
                    scope.Options.EnablePrivileges = true;

                    WqlEventQuery q = new WqlEventQuery();
                    q.EventClassName = "__InstanceOperationEvent";
                    q.WithinInterval = new TimeSpan(0, 0, 1);
                    q.Condition = @"TargetInstance ISA 'Win32_DiskDrive' ";
                    w = new ManagementEventWatcher(scope, q);
                    w.EventArrived += new EventArrivedEventHandler(w_EventArrived);
                    w.Start();
                }
            }

            public void StopListening()
            {
                w.Stop();
                w = null;
            }

            private void w_EventArrived(object sender, EventArrivedEventArgs e)
            {
                ManagementBaseObject baseObject = (ManagementBaseObject)e.NewEvent;

                var cls = baseObject.ClassPath.ClassName;
                if (cls.Equals("__InstanceCreationEvent") || cls.Equals("__InstanceDeletionEvent"))
                {
                    drives = null; // clear cache
                    Changed();
                }
            }

            public List<DiskDrive> Drives
            {
                get
                {
                    if (drives == null)
                        drives = Drives();
                    return drives;
                }
            }

        }
        static public readonly SharedDrives Shared = new SharedDrives();

        static public List<DiskDrive> Drives()
        {
            List<DiskDrive> ret = new List<DiskDrive>();
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_DiskDrive");
            foreach (var obj in searcher.Get())
            {
                try
                {
                    ret.Add(new DiskDrive(obj));
                }
                catch { }
            }
            return ret;
        }


        public ESourceType SourceType
        {
            get
            {
                return ESourceType.Drive;
            }
        }

        public long ReadSize
        {
            get
            {
                return LastInterestingSector;
            }
        }

        public long WriteSize
        {
            get
            {
                return (long)Size;
            }
        }

        public string ReadDescription
        {
            get
            {
                return String.Format("all partitions of disk {0} for a total of {1}", 
                    Model, 
                    HumanSizeConverter.HumanSize(ReadSize));
            }
        }
        public string WriteDescription
        {
            get
            {
                return String.Format("disk {0} with a total of {1} space available",
                    Model,
                    HumanSizeConverter.HumanSize(WriteSize));
            }
        }

        public virtual Stream ReadStream()
        {
            return DiskDriveStream.Create(this, true);
        }

        public virtual Stream WriteStream()
        {
            return DiskDriveStream.Create(this, false);
        }

        protected DiskDrive(ManagementBaseObject obj)
        {
            Index = (uint)obj["Index"];
            BytesPerSector = (uint)obj["BytesPerSector"]; // 512
            string t = CastOrDefault(obj, "MediaType", "");
            EMediaType mt;
            if (!mediaTypes.TryGetValue(t, out mt))
                mt = EMediaType.Unknown;
            MediaType = mt;
            t = CastOrDefault(obj, "InterfaceType", "");
            EInterfaceType et;
            if (!interfaceTypes.TryGetValue(t, out et))
                et = EInterfaceType.Unknown;
            InterfaceType = et;
            Model = CastOrDefault(obj, "Model", "Unknown model");
            DeviceID = (string)obj["DeviceID"]; // \\.\PHYSICALDRIVE%d
            Size = (ulong)obj["Size"]; // Size in bytes
            Partitions = new List<DiskPartition>();
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + DeviceID + "'} WHERE AssocClass = Win32_DiskDriveToDiskPartition");
            foreach (var sobj in searcher.Get())
            {
                try
                {
                    Partitions.Add(new DiskPartition(sobj));
                }
                catch { }
            }
        }

        public enum EMediaType
        {
            External,
            RemovableMedia,
            FixedHardDisk,
            Unknown
        };

        public enum EInterfaceType
        {
            Unknown,
            SCSI,
            HDC,
            IDE,
            USB,
            I1394
        };

        public uint Index { get; private set; }
        public uint BytesPerSector { get; private set; }
        public string DeviceID { get; private set; }
        public EInterfaceType InterfaceType { get; private set; }
        public EMediaType MediaType { get; private set; }
        public string Model { get; private set; }
        public ulong Size { get; private set; }
        public List<DiskPartition> Partitions { get; private set; }
        public long LastInterestingSector
        {
            get {
                ulong lastInterestingSector = 0;
                Partitions.ForEach(p =>
                {
                    if (p.StartingOffset + p.Size > lastInterestingSector)
                        lastInterestingSector = p.StartingOffset + p.Size;
                });
                return (long)lastInterestingSector;
            }
        }

        private static Dictionary<String, EMediaType> mediaTypes = new Dictionary<String, EMediaType>()
        {
            { "External hard disk media", EMediaType.External },
            { "Removable Media", EMediaType.RemovableMedia },
            { "Fixed hard disk media", EMediaType.FixedHardDisk },
        };

        private static Dictionary<String, EInterfaceType> interfaceTypes = new Dictionary<String, EInterfaceType>()
        {
            { "SCSI", EInterfaceType.SCSI },
            { "HDC", EInterfaceType.HDC },
            { "IDE", EInterfaceType.IDE },
            { "USB", EInterfaceType.USB },
            { "1394", EInterfaceType.I1394 }
        };
    }

    internal class DiskDriveStream : FileStream
    {
        private DiskDrive diskDrive;
        private SafeFileHandle handle;
        private bool readOnly;

        static internal DiskDriveStream Create(DiskDrive diskDrive, bool readOnly)
        {
            foreach (var v in diskDrive.Partitions)
            {
                v.Lock();
            }
            var handle = NativeMethods.CreateFile(diskDrive.DeviceID,
                readOnly ? NativeMethods.GENERIC_READ : NativeMethods.GENERIC_WRITE,
                0, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle.IsInvalid)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            return new DiskDriveStream(diskDrive, handle, readOnly);
        }

        public DiskDriveStream(DiskDrive diskDrive, SafeFileHandle handle, bool readOnly) : base(handle, readOnly ? FileAccess.Read : FileAccess.Write)
        {
            this.diskDrive = diskDrive;
            this.handle = handle;
            this.readOnly = readOnly;
        }
        
        protected override void Dispose(bool disposing)
        {
            foreach (var v in diskDrive.Partitions)
            {
                v.UnLock();
            }
            handle.Dispose();
            handle = null;
            base.Dispose(disposing);
        }

    }
}
