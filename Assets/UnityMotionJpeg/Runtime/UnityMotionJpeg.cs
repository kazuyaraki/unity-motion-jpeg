using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace KA.UnityMotionJpeg
{
    public class WriteSizeScope : IDisposable
    {
        private Stream m_Stream;
        private long m_DataPosition;

        protected WriteSizeScope()
        {
        }

        public WriteSizeScope(Stream stream)
        {
            Initialize(stream);
        }

        protected void Initialize(Stream stream)
        {
            m_Stream = stream;

            // Write dummy size.
            m_Stream.Write(0u);

            m_DataPosition = m_Stream.Position;
        }

        void IDisposable.Dispose()
        {
            m_Stream.Flush();

            // Write size.
            var curPos = m_Stream.Position;
            var size = (uint)(curPos - m_DataPosition);
            m_Stream.Seek(m_DataPosition - 4, SeekOrigin.Begin);
            m_Stream.Write(size);

            m_Stream.Seek(curPos, SeekOrigin.Begin);
        }
    }

    public class AVIChunkScope : WriteSizeScope
    {
        public AVIChunkScope(Stream stream, string fourCC)
        {
            var bytes = Encoding.ASCII.GetBytes(fourCC);
            stream.Write(bytes, 0, 4);

            Initialize(stream);
        }
    }

    public class AVIListScope : WriteSizeScope
    {
        private static readonly byte[] s_LIST = new byte[] { 0x4C, 0x49, 0x53, 0x54 };
        public AVIListScope(Stream stream, string fourCC)
        {
            stream.Write(s_LIST, 0, 4);

            Initialize(stream);

            var bytes = Encoding.ASCII.GetBytes(fourCC);
            stream.Write(bytes, 0, 4);
        }
    }

    public class Recorder
    {
        private static readonly byte[] s_LIST = new byte[] { 0x4C, 0x49, 0x53, 0x54 };
        private static readonly char[] s_RIFF = new char[] { 'R', 'I', 'F', 'F' };
        private static readonly char[] s_AVI = new char[] { 'A', 'V', 'I', ' ' };
        private static readonly byte[] s_00dc = new byte[] { 0x30, 0x30, 0x64, 0x63 };
        private static readonly byte[] s_AVIIF_KEYFRAME = new byte[] { 0x10, 0x00, 0x00, 0x00 };
        private const float jpegBpp = 8.25f;
        private const int jpegHeaderSize = 1024;
        private static byte[] m_ByteBuffer = null;
        private string m_Path;
        private FileStream m_AVIFile;
        private FileStream m_TempIndexFile;
        private string m_TempIndexPath;
        private uint m_TotalFrame = 0;
        private long m_TotalFramesPos;
        private long m_SizePos;
        private long m_LengthPos;
        private long m_MOVIPos;

        public void BeginRecording(string path, int width, int height, int frameRate)
        {
            var maxFrameSize = (int)(width * height * jpegBpp) + jpegHeaderSize;
            m_ByteBuffer = new byte[maxFrameSize];

            m_Path = path;
            m_AVIFile = File.Create(path);

            WriteCharArray(s_RIFF);

            // Write Dummy Size
            m_SizePos = m_AVIFile.Position;
            m_AVIFile.WriteByte(0);
            m_AVIFile.WriteByte(0);
            m_AVIFile.WriteByte(0);
            m_AVIFile.WriteByte(0);

            WriteCharArray(s_AVI);


            using (var hdrlList = new AVIListScope(m_AVIFile, "hdrl"))
            {
                using (var avihChunk = new AVIChunkScope(m_AVIFile, "avih"))
                {
                    // MicroSecPerFrame
                    m_AVIFile.Write(1000000 / (uint)frameRate);
                    // MaxBytesPerSec
                    m_AVIFile.Write(0u);
                    // PaddingGranularity
                    m_AVIFile.Write(0u);
                    // Flags
                    m_AVIFile.Write(0u);
                    // TotalFrames
                    m_TotalFramesPos = m_AVIFile.Position;
                    m_AVIFile.Write(0u);
                    // InitialFrames
                    m_AVIFile.Write(0u);
                    // Streams
                    m_AVIFile.Write(1u);
                    // SuggestedBufferSize
                    m_AVIFile.Write(0u);
                    // Width
                    m_AVIFile.Write((uint)width);
                    // Height
                    m_AVIFile.Write((uint)height);
                    // Dummy
                    m_AVIFile.Write(new byte[16], 0, 16);
                }

                using (var strlList = new AVIListScope(m_AVIFile, "strl"))
                {
                    using (var strhChunk = new AVIChunkScope(m_AVIFile, "strh"))
                    {
                        // fccType
                        m_AVIFile.WriteASCII("vids");
                        // fccHandler
                        m_AVIFile.WriteASCII("GPJM");
                        // Flags
                        m_AVIFile.Write(0u);
                        // Priority
                        m_AVIFile.Write((ushort)0);
                        // Language
                        m_AVIFile.Write((ushort)0);
                        // InitialFrames
                        m_AVIFile.Write(0u);
                        // Scale
                        m_AVIFile.Write(1u);
                        // Rate
                        m_AVIFile.Write((uint)frameRate);
                        // Start
                        m_AVIFile.Write(0u);
                        // Length
                        m_LengthPos = m_AVIFile.Position;
                        m_AVIFile.Write(0u);
                        // SuggestedBufferSize
                        m_AVIFile.Write(0u);
                        // Quality
                        m_AVIFile.Write(0u);
                        // SampleSize
                        m_AVIFile.Write(0u);
                        // Frame
                        m_AVIFile.Write((short)0);
                        m_AVIFile.Write((short)0);
                        m_AVIFile.Write((short)width);
                        m_AVIFile.Write((short)height);
                    }

                    using (var strhChunk = new AVIChunkScope(m_AVIFile, "strf"))
                    {
                        // Chunk size
                        m_AVIFile.Write(40u);
                        // Width
                        m_AVIFile.Write((uint)width);
                        // Height
                        m_AVIFile.Write((uint)height);
                        // Planes
                        m_AVIFile.Write((ushort)1);
                        // BitCount
                        m_AVIFile.Write((ushort)24);
                        // Compression
                        m_AVIFile.WriteASCII("MJPG");
                        // SizeImage
                        m_AVIFile.Write(0u);
                        // XPelsPerMeter
                        m_AVIFile.Write(0L);
                        // YPelsPerMeter
                        m_AVIFile.Write(0L);
                        // ClrUsed
                        m_AVIFile.Write(0u);
                        // ClrImportant
                        m_AVIFile.Write(0u);
                    }
                }
            }

            // ?
            using (var junkChunk = new AVIChunkScope(m_AVIFile, "JUNK"))
            {
                m_AVIFile.Write(new byte[12], 0, 12);
            }

            m_AVIFile.Write(s_LIST, 0, 4);
            // dummy size
            m_MOVIPos = m_AVIFile.Position;
            m_AVIFile.Write(0u);
            m_AVIFile.WriteASCII("movi");

            // Create index temporary file.
            m_TempIndexPath = path + ".idx.tmp";
            m_TempIndexFile = File.Create(m_TempIndexPath);
        }

        public void RecordFrame(Texture2D texture, int quality)
        {
            var jpeg = ImageConversion.EncodeToJPG(texture, quality);
            RecordFrame(jpeg);
        }

        public unsafe void RecordFrame(NativeArray<byte> jpeg)
        {
            // Copy NativeArray<byte> to byte[]
            if (m_ByteBuffer.Length < jpeg.Length)
            {
                m_ByteBuffer = new byte[jpeg.Length * 2];
            }
            var intPtr = (IntPtr)jpeg.GetUnsafeReadOnlyPtr<byte>();
            Marshal.Copy(intPtr, m_ByteBuffer, 0, jpeg.Length);

            RecordFrame(m_ByteBuffer, 0, jpeg.Length);
        }

        public void RecordFrame(byte[] jpeg)
        {
            RecordFrame(jpeg, 0, jpeg.Length);
        }

        public void RecordFrame(byte[] jpeg, int offset, int count)
        {
            long fileOffset = m_AVIFile.Position;
            m_AVIFile.Write(s_00dc, 0, 4);
            m_AVIFile.Write((uint)count);

            m_AVIFile.Write(m_ByteBuffer, offset, count);

            // Padding
            if (count % 2 == 1)
            {
                m_AVIFile.WriteByte(0);
            }

            // Write index
            m_TempIndexFile.Write(s_00dc, 0, 4);
            m_TempIndexFile.Write(s_AVIIF_KEYFRAME, 0, 4);
            m_TempIndexFile.Write((uint)fileOffset);
            m_TempIndexFile.Write((uint)count);

            m_TotalFrame++;
        }

        public void EndRecording()
        {
            // Write movi size and length.
            var moviEndPos = m_AVIFile.Position;
            m_AVIFile.Seek(m_MOVIPos, SeekOrigin.Begin);
            m_AVIFile.Write((uint)(moviEndPos - m_MOVIPos - 4));
            m_AVIFile.Seek(m_LengthPos, SeekOrigin.Begin);
            m_AVIFile.Write(m_TotalFrame);


            // Write index.
            m_AVIFile.Seek(moviEndPos, SeekOrigin.Begin);
            var indexLength = m_TempIndexFile.Position;
            using (var indexChunk = new AVIChunkScope(m_AVIFile, "idx1"))
            {
                m_TempIndexFile.Seek(0, SeekOrigin.Begin);
                var buffer = new byte[1024];
                while (true)
                {
                    var count = m_TempIndexFile.Read(buffer, 0, 1024);
                    m_AVIFile.Write(buffer, 0, count);
                    if (count < 1024)
                    {
                        break;
                    }
                }
                m_TempIndexFile.Close();

                File.Delete(m_TempIndexPath);
            }

            // Write file fize and frame count.
            var eofPos = m_AVIFile.Position;
            m_AVIFile.Seek(m_SizePos, SeekOrigin.Begin);
            m_AVIFile.Write((uint)(eofPos - m_SizePos - 4));
            m_AVIFile.Seek(m_TotalFramesPos, SeekOrigin.Begin);
            m_AVIFile.Write(m_TotalFrame);

            m_AVIFile.Close();

            Debug.LogFormat("AVI file saved. ({0})", m_Path);
        }

        private void WriteCharArray(char[] chars)
        {
            foreach (var c in chars)
            {
                var b = Convert.ToByte(c);
                m_AVIFile.WriteByte(b);
            }
        }
    }

    public static class Utility
    {
        public static void Write(this Stream stream, short value)
        {
            byte b;
            b = (byte)(value & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 8 & 0xff);
            stream.WriteByte(b);
        }

        public static void Write(this Stream stream, ushort value)
        {
            byte b;
            b = (byte)(value & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 8 & 0xff);
            stream.WriteByte(b);
        }

        public static void Write(this Stream stream, uint value)
        {
            byte b;
            b = (byte)(value & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 8 & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 16 & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 24 & 0xff);
            stream.WriteByte(b);
        }

        public static void Write(this Stream stream, long value)
        {
            byte b;
            b = (byte)(value & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 8 & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 16 & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 24 & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 32 & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 40 & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 48 & 0xff);
            stream.WriteByte(b);
            b = (byte)(value >> 56 & 0xff);
            stream.WriteByte(b);
        }

        public static void WriteASCII(this Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}