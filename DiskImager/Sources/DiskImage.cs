using liblzma_wrapper;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace DiskImager
{
    public interface IDiskImageType
    {
        string Description { get; }
        string[] Extensions { get; } 
        IDiskImage LoadImageAt(string path);
    }
    
    public interface IDiskImage : ISource
    {
        IDiskImageType Type { get; }
        string Path { get; }
    }
    
    public class DiskImageTypes
    {
        public static readonly IDiskImageType rawDiskImageType = new RawDiskImageType();
        public static readonly IDiskImageType zipDiskImageType = new ZipDiskImageType();
        public static readonly IDiskImageType lzmaDiskImageType = new LzmaDiskImageType();

        static public List<IDiskImageType> types = new List<IDiskImageType>()
        {
            rawDiskImageType,
            zipDiskImageType,
            lzmaDiskImageType
        };
    }

    public class RawDiskImageType : IDiskImageType
    {
        public string Description { get { return "Raw image"; } }
        public string[] Extensions { get { return new string[] { ".img", ".raw" }; } }

        public IDiskImage LoadImageAt(string path)
        {
            return new RawDiskImage(path);
        }
    }
    
    class RawDiskImage : IDiskImage
    {
        protected string path;
        protected long size;

        public RawDiskImage(string path)
        {
            this.path = path;
            try
            {
                FileInfo f = new FileInfo(path);
                this.size = f.Length;
            }
            catch (Exception) {
                this.size = -1;
            }
        }
                
        public ESourceType SourceType { get { return ESourceType.File; } }
        public virtual long ReadSize { get { return size; } }
        public long WriteSize
        {
            get
            {
                try
                {
                    FileInfo f = new FileInfo(path);
                    string folder = f.Directory.FullName;
                    if (!folder.EndsWith("\\"))
                        folder += '\\';

                    ulong free = 0, dummy1 = 0, dummy2 = 0;
                    if (NativeMethods.GetDiskFreeSpaceEx(folder, out free, out dummy1, out dummy2))
                    {
                        return (long)free;
                    }
                }
                catch {}
                return -1;
            }
        }

        public virtual string ReadDescription
        {
            get
            {
                return String.Format(Properties.Resources.ImageRawReadFormat,
                    HumanSizeConverter.HumanSize(ReadSize));
            }
        }
        public virtual string WriteDescription
        {
            get
            {
                return String.Format(Properties.Resources.ImageRawWriteFormat,
                    HumanSizeConverter.HumanSize(WriteSize));
            }
        }

        public virtual IDiskImageType Type { get { return DiskImageTypes.rawDiskImageType; } }
        public string Path { get { return path; } }

        public virtual Stream ReadStream()
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read);
        }        
        public virtual Stream WriteStream()
        {
            return new FileStream(path, FileMode.Create, FileAccess.Write);
        }
    }

    class ZipDiskImageType : IDiskImageType
    {
        public string Description { get { return "Zip image"; } }
        public string[] Extensions { get { return new string[] { ".img.zip", ".raw.zip" }; } }

        public IDiskImage LoadImageAt(string path)
        {
            return new ZipDiskImage(path);
        }
    }

    abstract class CompressedDiskImage : RawDiskImage
    {
        private long decompressedSize = -2;

        public CompressedDiskImage(string path) : base(path) { }

        protected abstract long DecompressedSize();

        public override long ReadSize
        {
            get
            {
                if (decompressedSize == -2)
                {
                    decompressedSize = -1;
                    try
                    {
                       decompressedSize = DecompressedSize();
                    }
                    catch { }
                }
                return decompressedSize;
            }
        }
    }

    class ZipDiskImage : CompressedDiskImage
    {
        public ZipDiskImage(string path) : base(path) { }

        public override IDiskImageType Type { get { return DiskImageTypes.zipDiskImageType; } }
        protected override long DecompressedSize()
        {
            FileStream img = new FileStream(path, FileMode.Open, FileAccess.Read);
            using (ZipArchive arc = new ZipArchive(img, ZipArchiveMode.Read))
            {
                if (arc.Entries.Count > 0)
                    return arc.Entries[0].Length;
            }
            return -1;
        }
        public override string ReadDescription
        {
            get
            {
                return String.Format(Properties.Resources.ImageZipReadFormat,
                    HumanSizeConverter.HumanSize(size),
                    HumanSizeConverter.HumanSize(ReadSize));
            }
        }
        public override string WriteDescription
        {
            get
            {
                return String.Format(Properties.Resources.ImageZipWriteFormat,
                    HumanSizeConverter.HumanSize(WriteSize));
            }
        }

        public override Stream ReadStream()
        {
            var archive = new ZipArchive(base.ReadStream(), ZipArchiveMode.Read, false);
            return new ZipArchiveStream(archive, archive.GetEntry("image.img").Open());
        }

        public override Stream WriteStream()
        {
            var archive = new ZipArchive(base.WriteStream(), ZipArchiveMode.Create, false);
            return new ZipArchiveStream(archive, archive.CreateEntry("image.img", CompressionLevel.Optimal).Open());
        }
    }

    internal class ZipArchiveStream : ProxyStream
    {
        private ZipArchive archive;

        public ZipArchiveStream(ZipArchive archive, Stream stream) : base(stream)
        {
            this.archive = archive;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            archive.Dispose();
        }
    }


    class LzmaDiskImageType : IDiskImageType
    {
        public string Description { get { return "xz image"; } }
        public string[] Extensions { get { return new string[] { ".img.xz", ".raw.xz" }; } }

        public IDiskImage LoadImageAt(string path)
        {
            return new LzmaDiskImage(path);
        }
    }

    class LzmaDiskImage : CompressedDiskImage
    {
        public LzmaDiskImage(string path) : base(path) {}

        public override IDiskImageType Type { get { return DiskImageTypes.lzmaDiskImageType; } }
        protected override long DecompressedSize()
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return (long)(new XZFileInfo(file)).uncompressed_size;
            }
        }

        public override string ReadDescription
        {
            get
            {
                return String.Format(Properties.Resources.ImageXZReadFormat,
                    HumanSizeConverter.HumanSize(size),
                    HumanSizeConverter.HumanSize(ReadSize));
            }
        }
        public override string WriteDescription
        {
            get
            {
                return String.Format(Properties.Resources.ImageXZWriteFormat,
                    HumanSizeConverter.HumanSize(WriteSize));
            }
        }
        
        public override Stream ReadStream()
        {
            return new LZMAStream(base.ReadStream(), false);
        }

        public override Stream WriteStream()
        {
            return new LZMAStream(base.WriteStream(), true);
        }
    }
}
