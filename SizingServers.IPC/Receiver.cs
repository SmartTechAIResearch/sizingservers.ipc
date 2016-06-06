/*
 * Copyright 2015 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace SizingServers.IPC {
    /// <summary>
    /// <para>Add a new Receiver in the code of the process you want to receive messages. Make sure the handles matches the one of the Sender.</para>
    /// </summary>
    public class Receiver : IDisposable {
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<MessageEventArgs> MessageReceived;

        private BinaryFormatter _bf;
        private TcpListener _tcpReceiver;

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
        /// The end point this receiver is listening on.
        /// </summary>
        public EndPoint LocalEndPoint { get { return _tcpReceiver?.LocalEndpoint; } }
        /// <summary>
        /// <para>This is an optional parameter in the constructor.</para>
        /// <para>If you don't use it, receiver end points are stored in the Windows registry and IPC communication is only possible for processes running under the current local user.</para>
        /// <para>If you do use it, these end points are fetched from a Windows service over tcp, making it a distributed IPC.This however will be slower and implies a security risk since there will be network traffic.</para>
        /// </summary>
        public EndPointManagerServiceConnection EndPointManagerServiceConnection { get; private set; }

        /// <summary>
        /// <para>Receives messages of a Sender having the same handle.</para>
        /// <para>When using the end point manager service, some security measures are advised.</para>
        /// <para>You can use Shared.Encrypt(...) (and Shared.Decrypt(...)) to encrypt your received messages (if they are strings) via the MessageReceived event.</para>
        /// <para>Alternatively you can use a ssh tunnel, that will probably be safer and faster</para>
        /// </summary>
        /// <param name="handle">
        /// <para>The handle is a value shared by a Sender and its Receivers.  ; , * + and - cannot be used!</para>
        /// <para>It links both parties so messages from a Sender get to the right Receivers.</para>
        /// <para>Make sure this is a unique value: use a GUID for instance:</para>
        /// <para>There is absolutely no checking to see if this handle is used in another Sender - Receivers relation.</para>
        /// </param>
        /// <param name="ipAddressToRegister">
        /// <para>This parameter is only applicable if you are using a end point manager service.</para>
        /// <para>A receiver listens to all available IPs for connections. The ip that is registered on the end point manager (service) is by default automatically determined.</para>
        /// <para>However, this does not take into account that senders, receiver or end point manager services are possibly not on the same network.</para>
        /// <para>Therefor you can override this behaviour by supplying your own IP that will be registered to the end point manager service.</para>
        /// </param>
        /// <param name="endPointManagerServiceConnection">
        /// <para>This is an optional parameter.</para>
        /// <para>If you don't use it, receiver end points are stored in the Windows registry and IPC communication is only possible for processes running under the current local user.</para>
        /// <para>If you do use it, these end points are fetched from a Windows service over tcp, making it a distributed IPC.This however will be slower and implies a security risk since there will be network traffic.</para>
        /// </param>
        public Receiver(string handle, IPAddress ipAddressToRegister = null, EndPointManagerServiceConnection endPointManagerServiceConnection = null) {
            Handle = handle;
            EndPointManagerServiceConnection = endPointManagerServiceConnection;

            for (int i = 0; ; i++) //Try 3 times.
                try {
                    _tcpReceiver = new TcpListener(EndPointManager.RegisterReceiver(Handle, ipAddressToRegister, EndPointManagerServiceConnection));
                    _tcpReceiver.Start(endPointManagerServiceConnection == null ? 1 : 2); //Keep one connection open to enable the service pinging it.
                    break;
                }
                catch (EndPointManagerServiceConnectionException) {
                    throw;
                }
                catch (Exception) {
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
                            HandleReceive(_tcpReceiver.AcceptTcpClient());
                        }
                        catch {
                            if (!IsDisposed)
                                throw;
                        }
                    }
                    else {
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
                    while (!IsDisposed && client != null && client.Connected) {
                        Stream str = client.GetStream();

                        long handleSize = Shared.GetLong(Shared.ReadBytes(str, client.ReceiveBufferSize, Shared.LONGSIZE));
                        string handle = Shared.GetString(Shared.ReadBytes(str, client.ReceiveBufferSize, handleSize));

                        if (handle == Handle) {
                            bool messageIsByteArray = Shared.GetBool(Shared.ReadBytes(str, client.ReceiveBufferSize, 1));
                            long messageSize = Shared.GetLong(Shared.ReadBytes(str, client.ReceiveBufferSize, Shared.LONGSIZE));

                            byte[] messageBytes = Shared.ReadBytes(str, client.ReceiveBufferSize, messageSize);

                            object message = messageIsByteArray ? messageBytes : Shared.GetObject(messageBytes, _bf);

                            MessageReceived?.Invoke(this, new MessageEventArgs() { Handle = Handle, Message = message });
                        }
                        else {
                            //Invalid sender or ping from EPM service. Close the connection.
                            client.Dispose();
                        }
                    }
                }
                catch {
                    //Not important. If it doesn't work the sender does not exist anymore or the sender will handle it.
                }
            }, null);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose() {
            if (!IsDisposed) {
                IsDisposed = true;

                if (_tcpReceiver != null) {
                    _tcpReceiver.Stop();
                    _tcpReceiver = null;
                }
                _bf = null;

                Handle = null;
            }
        }
    }
}
