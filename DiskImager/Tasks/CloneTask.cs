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
        internal CloneProgression(long total)
        {
            this.total = total;
        }
        public long written = 0;
        public long stepBytesPerSeconds = 0;
        public long avgBytesPerSeconds = 0;
        public long elapsed = 0;
        public long total;

        internal void Report(IProgress<CloneProgression> progress, long stepRead, long lastElapsed, long elapsed)
        {
            if (elapsed > lastElapsed) // prevent division by zero
            {
                this.avgBytesPerSeconds = (written * 1000) / elapsed;
                this.stepBytesPerSeconds = (stepRead * 1000) / (elapsed - lastElapsed);
                this.elapsed = elapsed;
            }
            progress.Report(this);
        }
    };

    class CloneTask
    {
        public delegate void ForEachRead(byte[] buffer, long size);
        delegate long AsyncRead(out byte[] buffer);
        public delegate void EndCallback();
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

        /// <summary>
        /// read size bytes of data from the src stream to the dst stream
        /// </summary>
        /// <param name="src">Stream that contains the data to read</param>
        /// <param name="dst">Stream where the data will be written</param>
        /// <param name="size">Amount of data to clone in bytes</param>
        /// <param name="cancellationToken">Token to use for cancelling the clone</param>
        /// <param name="progress">Progression reporter</param>
        /// <param name="progressStepDuration">Amount of time in ms between to progression report</param>
        /// <returns>true if the clone task completed, false otherwise</returns>
        static public bool clone(
            Stream src, Stream dst, long size, 
            CancellationToken cancellationToken, 
            IProgress<CloneProgression> progress, long progressStepDuration = 100)
        {
            return foreachRead((byte[] buffer, long read) =>
            {
                dst.Write(buffer, 0, (int)read);
            }, () =>
            {
                dst.Flush();
            }, src, size, cancellationToken, progress, progressStepDuration);
        }

        static public bool foreachRead(
            ForEachRead callForEachRead,
            EndCallback then,
            Stream src, long size,
            CancellationToken cancellationToken,
            IProgress<CloneProgression> progress, long progressStepDuration = 100)
        {
            Stopwatch watch = Stopwatch.StartNew();
            long lastElapsed = 0;
            byte[] buffer;
            long read, stepRead = 0;
            CloneProgression p = new CloneProgression(size < 0 ? src.Length : size);
            EndCallback endCallback;
            AsyncRead asyncRead = syncReader(src, p.total, out endCallback, cancellationToken);
            while (!cancellationToken.IsCancellationRequested && (read = asyncRead(out buffer)) > 0)
            {
                callForEachRead(buffer, read);
                p.written += read;
                stepRead += read;
                long elapsed = watch.ElapsedMilliseconds;
                if (elapsed - lastElapsed >= progressStepDuration)
                {
                    p.Report(progress, stepRead, lastElapsed, elapsed);
                    stepRead = 0;
                    lastElapsed = elapsed;
                }
            }

            watch.Stop();
            then();
            endCallback();
            if (p.written == p.total)
            {
                p.Report(progress, stepRead, lastElapsed, watch.ElapsedMilliseconds);
            }
            return p.written == p.total;
        }
    }
}
