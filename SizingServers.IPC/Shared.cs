/*
 * Copyright 2016 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace SizingServers.IPC {
    /// <summary>
    /// Shared functions for internal and external use.
    /// </summary>
    public static class Shared {

        /// <summary>
        /// UTF8
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static byte[] GetBytes(string s) { return Encoding.UTF8.GetBytes(s); }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="l"></param>
        /// <returns></returns>
        public static byte[] GetBytes(long l) {
            int size = Marshal.SizeOf(l);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(l, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public static byte GetByte(bool b) {
            return (byte)(b ? 1 : 0);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="o"></param>
        /// <param name="bf"></param>
        /// <returns></returns>
        public static byte[] GetBytes(object o, BinaryFormatter bf) {
            byte[] bytes = null;
            using (var ms = new MemoryStream()) {
                bf.Serialize(ms, o);
                bytes = ms.GetBuffer();
            }
            return bytes;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static long GetLong(byte[] bytes) {
            long l = Activator.CreateInstance<long>();
            int size = Marshal.SizeOf(l);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(bytes, 0, ptr, size);
            l = (long)Marshal.PtrToStructure(ptr, l.GetType());
            Marshal.FreeHGlobal(ptr);
            return l;
        }
        /// <summary>
        /// UTF8
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string GetString(byte[] bytes) { return Encoding.UTF8.GetString(bytes); }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static bool GetBool(byte[] bytes) { return bytes[0] == 1; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="bf"></param>
        /// <returns></returns>
        public static object GetObject(byte[] bytes, BinaryFormatter bf) {
            object o;
            using (var ms = new MemoryStream(bytes))
                o = bf.Deserialize(ms);
            return o;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="bufferSize"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static byte[] ReadBytes(Stream str, int bufferSize, long length) {
            var bytes = new byte[length];

            long totalRead = 0;
            while (totalRead != length) {
                int chunkLength = bufferSize;
                if (chunkLength > length - totalRead) chunkLength = (int)(length - totalRead);

                var chunk = new byte[chunkLength];
                int chunkRead = str.Read(chunk, 0, chunkLength);
                if (chunkRead <= 0) break;
                chunk.CopyTo(bytes, totalRead);
                totalRead += chunkRead;
            }

            return bytes;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="bufferSize"></param>
        /// <param name="bytes"></param>
        public static void WriteBytes(Stream str, int bufferSize, byte[] bytes) {
            int offset = 0;
            while (offset != bytes.Length) {
                int length = bufferSize;
                if (offset + length > bytes.Length)
                    length = bytes.Length - offset;

                str.Write(bytes, offset, length);

                offset += length;
            }

            str.Flush();
        }
    }
}
