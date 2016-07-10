using System.IO;

namespace DiskImager
{
    public class ProxyStream : Stream
    {
        private Stream stream;

        public ProxyStream(Stream stream)
        {
            this.stream = stream;
        }

        public override bool CanRead { get { return stream.CanRead; } }
        public override bool CanSeek { get { return stream.CanSeek; } }
        public override bool CanWrite { get { return stream.CanWrite; } }
        public override long Length { get { return stream.Length; } }
        public override long Position { get { return stream.Position; } set { stream.Position = value; } }
        public override void Flush() { stream.Flush(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            stream.Dispose();
        }
    }
}
