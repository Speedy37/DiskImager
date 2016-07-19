using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiskImager.Tasks
{
    class ChecksumTask
    {
        static public string checksum(
            Stream src, long size,
            HashAlgorithm hashAlgorithm,
            CancellationToken cancellationToken,
            IProgress<CloneProgression> progress, long progressStepDuration = 100)
        {
            bool ok = CloneTask.foreachRead((byte[] buffer, long read) =>
            {
                hashAlgorithm.TransformBlock(buffer, 0, (int)read, null, 0);
            }, () =>
            {
                byte[] buffer = new byte[0];
                hashAlgorithm.TransformFinalBlock(buffer, 0, 0);
            }, src, size, cancellationToken, progress, progressStepDuration);
            return ok ? ToHex(hashAlgorithm.Hash) : null;
        }

        static string[] HexTbl = Enumerable.Range(0, 256).Select(v => v.ToString("X2")).ToArray();
        static string ToHex(byte[] array)
        {
            StringBuilder s = new StringBuilder(array.Length * 2);
            foreach (var v in array)
                s.Append(HexTbl[v]);
            return s.ToString();
        }
    }
}
