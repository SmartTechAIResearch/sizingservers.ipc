/*
 * Copyright 2015 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace SizingServers.IPC {
    /// <summary>
    /// <para>Add a new Sender in the code of the process you want to send messages. Make sure the handles matches the one of the Receivers.</para>
    /// <para>This inter process communication only works on the same machine and in the same Windows session.</para>
    /// <para>Suscribe to OnSendFailed for error handeling. Please not Sending will always fail when a Receiver disappears.</para>
    /// </summary>
    public class Sender : IDisposable {
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<MessageEventArgs> BeforeMessageSent;
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<MessageEventArgs> AfterMessageSent;
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<ErrorEventArgs> OnSendFailed;

        private BinaryFormatter _bf;
        private Dictionary<TcpClient, IPEndPoint> _senders;

        private readonly object _lock = new object();

        /// <summary>
        /// Hashcode of the message. When resending the same data it is not serialized again.
        /// </summary>
        private int _hashcode;
        private byte[] _bytes;


        /// <summary>
        /// </summary>
        public bool IsDisposed { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public string Handle { get; private set; }
        /// <summary>
        /// <para>When true, a message (+ encapsulation) you send is kept in memory. When you resend the same message it will not be serialized again.</para>
        /// </summary>
        public bool Buffered { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public Settings Settings { get; private set; }

        /// <summary>
        /// <para>Add a new Sender in the code of the process you want to send messages. Make sure the handles matches the one of the Receivers.</para>
        /// <para>This inter process communication only works on the same machine and in the same Windows session.</para>
        /// <para>Suscribe to OnSendFailed for error handeling. Please not Sending will always fail when a Receiver disappears.</para>
        /// </summary>
        /// <param name="handle">A unique identifier to match the right sender with the right receivers.</param>
        /// <param name="buffered">
        /// <para>When true, a message (+ encapsulation) you send is kept in memory. When you resend the same message it will not be serialized again.</para>
        /// </param>
        /// <param name="settings"></param>
        public Sender(string handle, bool buffered = false, Settings settings = null) {
            if (string.IsNullOrWhiteSpace(handle)) throw new ArgumentNullException(handle);

            Handle = handle;
            Buffered = buffered;
            Settings = settings;

            _senders = new Dictionary<TcpClient, IPEndPoint>();
            _bf = new BinaryFormatter();
        }
        /// <summary>
        /// Send a message to the Receivers. This is a blocking function.
        /// </summary>
        /// <param name="message">
        /// If the given object is a byte array, it will not be serialized. Otherwise, the object will be serialized using a binary formatter.
        /// </param>
        public void Send(object message) {
            lock (_lock)
               try {
                    if (!IsDisposed) {
                        BeforeMessageSent?.Invoke(this, new MessageEventArgs() { Message = message });

                        SetSenders();
                        if (_senders.Count != 0)
                            if (Buffered) {
                                if (message.GetHashCode() != _hashcode) {
                                    _bytes = SerializeMessage(message);
                                    _hashcode = message.GetHashCode();
                                }
                                Parallel.ForEach(_senders, (kvp) => Send(kvp.Key, kvp.Value, _bytes));

                            } else {
                                Parallel.ForEach(_senders, (kvp) => Send(kvp.Key, kvp.Value, SerializeMessage(message)));
                            }

                        AfterMessageSent?.Invoke(this, new MessageEventArgs() { Message = message });
                    }
                } catch (Exception ex) {
                    if (!IsDisposed && OnSendFailed != null) OnSendFailed(this, new ErrorEventArgs(ex));
                }
        }

        /// <summary>
        /// Writes the handle size, the handle (UTF8 encoding), 1 if message is byte array or 0, the message size and the message to an array.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private byte[] SerializeMessage(object message) {
            bool messageIsByteArray = message is byte[];

            byte[] handleBytes = Shared.GetBytes(Handle);
            byte[] handleSizeBytes = Shared.GetBytes(handleBytes.LongLength);

            byte[] messageBytes = messageIsByteArray ? message as byte[] : Shared.GetBytes(message, _bf);
            byte[] messageSizeBytes = Shared.GetBytes(messageBytes.LongLength);

            var bytes = new byte[handleSizeBytes.LongLength + handleBytes.LongLength + 1 + messageSizeBytes.LongLength + messageBytes.LongLength];

            long pos = 0L;
            handleSizeBytes.CopyTo(bytes, pos);
            pos += handleSizeBytes.LongLength;

            handleBytes.CopyTo(bytes, pos);
            pos += handleBytes.LongLength;

            bytes[pos++] = Shared.GetByte(messageIsByteArray);

            messageSizeBytes.CopyTo(bytes, pos);
            pos += messageSizeBytes.LongLength;

            messageBytes.CopyTo(bytes, pos);

            return bytes;
        }

        /// <summary>
        /// Clean up the stored tcp clients (_senders) and add new ones if need be.
        /// </summary>
        private void SetSenders() {
            var newSenders = new Dictionary<TcpClient, IPEndPoint>();
            foreach (IPEndPoint endPoint in EndPointManager.GetReceiverEndPoints(Handle, Settings)) {
                bool senderFound = false;
                foreach (TcpClient sender in _senders.Keys)
                    if (_senders[sender].Port == endPoint.Port) {
                        newSenders.Add(sender, endPoint);
                        senderFound = true;
                        break;
                    }

                if (!senderFound)
                    newSenders.Add(new TcpClient(), endPoint);
            }

            foreach (TcpClient oldSender in _senders.Keys) {
                bool equals = false;
                foreach (TcpClient sender in newSenders.Keys)
                    if (_senders[oldSender].Port == newSenders[sender].Port) {
                        equals = true;
                        break;
                    }
                if (!equals)
                    oldSender.Dispose();
            }

            _senders = newSenders;
        }

        private void Send(TcpClient sender, IPEndPoint endPoint, byte[] bytes) {
            try {
                if (!sender.Connected)
                    try {
                        sender.Connect(endPoint);
                    } catch {
                        return;
                    }

                Shared.WriteBytes(sender.GetStream(), sender.SendBufferSize, bytes);

            } catch (Exception ex) {
                if (!IsDisposed && OnSendFailed != null) OnSendFailed(this, new ErrorEventArgs(ex));
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void Dispose() {
            if (!IsDisposed) {
                IsDisposed = true;
                if (_senders != null) {
                    foreach (TcpClient sender in _senders.Keys)
                        sender.Dispose();
                    _senders = null;
                }

                _bf = null;
                Handle = null;
                _bytes = null;
            }
        }
    }
}
