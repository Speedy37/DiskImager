using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiskImager
{
    public class CloneProgression
    {
        internal CloneProgression(ulong totalBytes)
        {
            this.totalBytes = totalBytes;
            this.clonedBytes = 0;
            this.bytesPerSeconds = 0;
        }
        public ulong clonedBytes;
        public ulong totalBytes;
        public ulong bytesPerSeconds;
    };

    class CloneTask
    {
        delegate long AsyncRead(out byte[] buffer);
        delegate void EndCallback();
        class ReadAhead
        {
            internal byte[] buffer;
            internal long read;
        }

        static AsyncRead syncReader(Stream src, long end, out EndCallback endCallback, CancellationToken cancellationToken)
        {
            long bufferPosition = 0;
            byte[] buffer = new byte[65536];

            endCallback = () => { };

            return (out byte[] toBuffer) =>
            {
                toBuffer = buffer;
                if (bufferPosition >= end)
                    return 0;
                int size = (int)Math.Min(buffer.Length, end - bufferPosition);
                int read = src.Read(buffer, 0, size);
                bufferPosition += read;
                return read;
            };
        }

        static AsyncRead asyncReader(Stream src, long end, out EndCallback endCallback, CancellationToken cancellationToken)
        {
            bool exited = false;
            object locker = new object();
            long ReadSize = 65536;
            long ahead = 0;
            long buffer = 0;
            long bufferPosition = 0;
            ReadAhead[] buffers = new ReadAhead[16];
            for (int i = 0, len = buffers.Length; i < len; ++i)
            {
                ReadAhead b = buffers[i] = new ReadAhead();
                b.buffer = new byte[ReadSize];
                b.read = 0;
            }

            Task.Run(() =>
            {
                while (bufferPosition < end && !cancellationToken.IsCancellationRequested)
                {
                    ReadAhead b = buffers[buffer % buffers.Length];
                    b.read = src.Read(b.buffer, 0, (int)Math.Min(ReadSize, end - bufferPosition));
                    if (b.read > 0)
                    {
                        bufferPosition += b.read;
                        lock (locker)
                        {
                            buffer++;
                            Monitor.Pulse(locker);
                            if (buffer - ahead >= buffers.Length) // wait until a read is done
                                Monitor.Wait(locker);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                lock (locker)
                {
                    exited = false;
                }
            }, cancellationToken);

            endCallback = () =>
            {
                lock (locker)
                {
                    Monitor.Pulse(locker);
                    while (!exited)
                        Monitor.Wait(locker);
                }
            };

            return (out byte[] toBuffer) =>
            {
                ReadAhead b;
                lock (locker)
                {
                    while (ahead == buffer)
                    {
                        if (cancellationToken.IsCancellationRequested) {
                            toBuffer = null;
                            return 0;
                        }
                        Monitor.Wait(locker);
                    }
                    b = buffers[ahead++ % buffers.Length];
                    Monitor.Pulse(locker);
                }
                toBuffer = b.buffer;
                return b.read;
            };
        }

        static public bool clone(Stream src, Stream dst, long size, IProgress<CloneProgression> progress, CancellationToken cancellationToken)
        {
            byte[] buffer;
            Stopwatch wall = Stopwatch.StartNew();
            Stopwatch wstep = Stopwatch.StartNew();
            long read;
            ulong stepProgression = 0;
            ulong stepSize = 0;
            CloneProgression p = new CloneProgression((ulong)(size < 0 ? src.Length : size));
            EndCallback endCallback;
            AsyncRead asyncRead = syncReader(src, (long)p.totalBytes, out endCallback, cancellationToken);
            while (!cancellationToken.IsCancellationRequested && (read = asyncRead(out buffer)) > 0)
            {
                dst.Write(buffer, 0, (int)read);
                p.clonedBytes += (ulong)read;
                stepSize += (ulong)read;
                ulong progression = (p.clonedBytes * 1000) / p.totalBytes;
                if (stepProgression != progression && wstep.ElapsedMilliseconds > 0)
                {
                    wstep.Stop();
                    p.bytesPerSeconds = (stepSize * 1000) / (ulong)wstep.ElapsedMilliseconds;
                    progress.Report(p);
                    wstep.Restart();
                    stepProgression = progression;
                    stepSize = 0;
                }
            }

            wall.Stop();
            wstep.Stop();
            if (p.clonedBytes == p.totalBytes)
            {
                p.bytesPerSeconds = (p.totalBytes * 1000) / Math.Max((ulong)wall.ElapsedMilliseconds, 1);
                progress.Report(p);
            }
            dst.Flush();
            endCallback();
            return p.clonedBytes == p.totalBytes;
        }
    }
}
