/*
 * Copyright 2015 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace SizingServers.IPC {
    /// <summary>
    /// <para>Add a new Receiver in the code of the process you want to receive messages. Make sure the handles matches the one of the Sender.</para>
    /// <para>This inter process communication only works on the same machine and in the same Windows session.</para>
    /// </summary>
    public class Receiver : IDisposable {
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<MessageEventArgs> MessageReceived;

        private BinaryFormatter _bf;
        private TcpListener _receiver;

        /// <summary>
        /// 
        /// </summary>
        public bool IsDisposed { get; private set; }
        /// <summary>
        /// <para>The handle is a value shared by a Sender and its Receivers.</para>
        /// <para>It links both parties so messages from a Sender get to the right Receivers.</para>
        /// <para>Make sure this is a unique value: use a GUID for instance:</para>
        /// <para>There is absolutely no checking to see if this handle is used in another Sender - Receivers relation.</para>
        /// </summary>
        public string Handle { get; private set; }

        /// <summary>
        /// Receives messages of a Sender having the same handle.
        /// </summary>
        /// <param name="handle">
        /// <para>The handle is a value shared by a Sender and its Receivers.</para>
        /// <para>It links both parties so messages from a Sender get to the right Receivers.</para>
        /// <para>Make sure this is a unique value: use a GUID for instance:</para>
        /// <para>There is absolutely no checking to see if this handle is used in another Sender - Receivers relation.</para>
        /// </param>
        public Receiver(string handle) {
            Handle = handle;

            for (int i = 0; ; i++) //Try 3 times.
                try {
                    _receiver = new TcpListener(EndPointManager.RegisterReceiver(Handle));
                    _receiver.Start(1);
                    break;
                } catch {
                    //Not important. If it doesn't work the sender does not exist anymore or the sender will handle it.
                }

            _bf = new BinaryFormatter();

            BeginReceive();
        }

        private void BeginReceive() {
            ThreadPool.QueueUserWorkItem((state) => {
                while (!IsDisposed)
                    if (MessageReceived != null) {
                        try {
                            HandleReceive(_receiver.AcceptTcpClient());
                        } catch {
                            if (!IsDisposed) throw;
                        }
                    } else {
                        Thread.Sleep(1);
                    }
            }, null);
        }

        /// <summary>
        /// <para>Reads handle size, handle, 1 if message is byte array or 0, message size and message from the stream.</para>
        /// <para>If the handle in the message is invalid the connection will be closed.</para>
        /// </summary>
        /// <param name="client"></param>
        private void HandleReceive(TcpClient client) {
            ThreadPool.QueueUserWorkItem((state) => {
                try {
                    while (!IsDisposed) {
                        Stream str = client.GetStream();
                        int longSize = Marshal.SizeOf<long>();

                        long handleSize = GetLong(ReadBytes(str, client.ReceiveBufferSize, longSize));
                        string handle = GetString(ReadBytes(str, client.ReceiveBufferSize, handleSize));

                        if (handle == Handle) {
                            bool messageIsByteArray = GetBool(ReadBytes(str, client.ReceiveBufferSize, 1));
                            long messageSize = GetLong(ReadBytes(str, client.ReceiveBufferSize, longSize));

                            byte[] messageBytes = ReadBytes(str, client.ReceiveBufferSize, messageSize);

                            object message = messageIsByteArray ? messageBytes : GetObject(messageBytes);

                            if (MessageReceived != null)
                                MessageReceived(this, new MessageEventArgs() { Message = message });
                        } else {
                            //Invalid sender. Close the connection.
                            client.Dispose();
                        }
                    }
                } catch {
                    //Not important. If it doesn't work the sender does not exist anymore or the sender will handle it.
                }
            }, null);
        }

        private byte[] ReadBytes(Stream str, int bufferSize, long length) {
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

        private long GetLong(byte[] bytes) {
            long l = Activator.CreateInstance<long>();
            int size = Marshal.SizeOf(l);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(bytes, 0, ptr, size);
            l = (long)Marshal.PtrToStructure(ptr, l.GetType());
            Marshal.FreeHGlobal(ptr);
            return l;
        }
        private string GetString(byte[] bytes) { return Encoding.UTF8.GetString(bytes); }
        private bool GetBool(byte[] bytes) { return bytes[0] == 1; }
        private object GetObject(byte[] bytes) {
            object o;
            using (var ms = new MemoryStream(bytes))
                o = _bf.Deserialize(ms);
            return o;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose() {
            if (!IsDisposed) {
                IsDisposed = true;

                if (_receiver != null) {
                    _receiver.Stop();
                    _receiver = null;
                }
                _bf = null;

                Handle = null;
            }
        }
    }
}
