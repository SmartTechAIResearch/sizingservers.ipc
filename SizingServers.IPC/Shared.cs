using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace SizingServers.IPC {
    /// <summary>
    /// Shared functions for internal and external use.
    /// </summary>
    public static class Shared {

        #region Serialization and stream handling 

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
        #endregion

        #region Storing and retreiving tcp endpoints
        /// <summary>
        /// Get endpoints from the registry.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static Dictionary<string, KeyValuePair<string, HashSet<int>>> GetRegisteredEndPoints(Settings settings) {
            //Get the end points from the registry.
            var endPoints = new Dictionary<string, KeyValuePair<string, HashSet<int>>>();

            string value = settings == null ? GetRegisteredEndPoints() : SendAndReceiveEPM(string.Empty, settings.GetClient());
            if (value.Length != 0) {
                string[] split = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string token in split) {
                    string[] kvp = token.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    string handle = kvp[0];

                    string[] kvpConnection = kvp[1].Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    string hostname = kvpConnection[0];
                    var ports = kvpConnection[1].Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);

                    var hs = new HashSet<int>();
                    foreach (string port in ports)
                        hs.Add(int.Parse(port));

                    endPoints.Add(handle, new KeyValuePair<string, HashSet<int>>(hostname, hs));
                }
            }

            return endPoints;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static string GetRegisteredEndPoints() {
            RegistryKey subKey = Registry.CurrentUser.OpenSubKey("Software\\" + EndPointManager.KEY);
            if (subKey != null)
                return subKey.GetValue("EndPoints") as string;

            return string.Empty;
        }
        
        /// <summary>
        /// Set endpoints to the registery.
        /// </summary>
        /// <param name="endPoints"></param>
        /// <param name="settings"></param>
        public static void SetRegisteredEndPoints(Dictionary<string, KeyValuePair<string, HashSet<int>>> endPoints, Settings settings) {
            var sb = new StringBuilder();
            foreach (string handle in endPoints.Keys) {
                sb.Append(handle);
                sb.Append(':');

                var kvpConnection = endPoints[handle];
                sb.Append(kvpConnection.Key);
                sb.Append('-');

                foreach (int port in kvpConnection.Value) {
                    sb.Append(port);
                    sb.Append('+');
                }

                sb.Append(',');
            }

            if (settings == null)
                SetRegisteredEndPoints(sb.ToString());
            else
                SendAndReceiveEPM(sb.ToString(), settings.GetClient());
        }
        /// <summary>
        /// Set endpoints to the registery.
        /// </summary>
        /// <param name="endPoints"></param>
        public static void SetRegisteredEndPoints(string endPoints) {
            RegistryKey subKey = Registry.CurrentUser.CreateSubKey("Software\\" + EndPointManager.KEY, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.Volatile);
            subKey.SetValue("Endpoints", endPoints, RegistryValueKind.String);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message">Empty string to get the end points or the end point represeted as a string to set them.</param>
        /// <returns></returns>
        private static string SendAndReceiveEPM(string message, TcpClient client) {
            try {
                //Serialize message
                byte[] messageBytes = GetBytes(message);
                byte[] messageSizeBytes = GetBytes(messageBytes.LongLength);
                byte[] bytes = new byte[messageSizeBytes.LongLength + messageBytes.LongLength];

                long pos = 0L;
                messageSizeBytes.CopyTo(bytes, pos);
                pos += messageSizeBytes.LongLength;
                messageBytes.CopyTo(bytes, pos);

                Stream str = client.GetStream();

                WriteBytes(str, client.SendBufferSize, bytes);

                int longSize = Marshal.SizeOf<long>();
                long messageSize = GetLong(ReadBytes(str, client.ReceiveBufferSize, longSize));

                return GetString(ReadBytes(str, client.ReceiveBufferSize, messageSize));
            }
            catch {
                throw;
            }
        }
        #endregion
    }
}
