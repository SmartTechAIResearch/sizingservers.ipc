/*
 * Copyright 2016 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

namespace SizingServers.IPC.EndPointManagerService {
    /// <summary>
    /// Serves at storing and providing receiver end points so communication between senders and receivers can be established.
    /// </summary>
    public partial class Service : ServiceBase {
        /// <summary>
        /// Default port for the service to listen on.
        /// </summary>
        public const int DEFAULT_TCP_PORT = 4455;

        private TcpListener _receiver;
        private TcpListener _receiverv6;

        private int _port = DEFAULT_TCP_PORT;
        private bool _isDisposed;
        private string _registeredEndPoints = string.Empty;

        /// <summary>
        /// Serves at storing and providing receiver end points so communication between senders and receivers can be established.
        /// </summary>
        public Service(string[] args) {
            InitializeComponent();
            eventLog.Source = ServiceName;
            HandleArgs(args);
        }

        /// <summary>
        /// For debugging purposes only.
        /// </summary>
        public void Start() { OnStart(null); }


        private void HandleArgs(string[] args) {
            if (args == null || args.Length == 0) {
                eventLog.WriteEntry("No port in startup arguments given. Attempting to listen on the default tcp port (" + _port + ").");
            }
            else {
                if (int.TryParse(args[0], out _port))
                    eventLog.WriteEntry("Attempting to listen on tcp port " + _port + ".");
                else
                    eventLog.WriteEntry("Failed to get the tcp port in the startup arguments. The given argument cannot be parsed to an integer. Attempting to listen on the default tcp port (" + _port + ").", EventLogEntryType.Warning);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args) {
            HandleArgs(args);

            try {
                _receiver = new TcpListener(IPAddress.Any, _port);
                _receiver.Start();

                _receiverv6 = new TcpListener(IPAddress.IPv6Any, _port);
                _receiverv6.Start();

                eventLog.WriteEntry("Listening on tcp port " + _port + ".");
            }
            catch (Exception ex) {
                eventLog.WriteEntry("Failed to listen on tcp port " + _port + ". " + ex, EventLogEntryType.Warning);
            }

            BeginReceive(_receiver);
            BeginReceive(_receiverv6);
        }

        private void BeginReceive(TcpListener receiver) {
            ThreadPool.QueueUserWorkItem((state) => {
                while (!_isDisposed)
                    try {
                        HandleReceive(receiver.AcceptTcpClient());
                    }
                    catch (Exception ex) {
                        if (!_isDisposed)
                            eventLog.WriteEntry("Failed handeling endpoint request. " + ex, EventLogEntryType.Error);

                        break;
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
                    while (!_isDisposed) {
                        Stream str = client.GetStream();
                        int longSize = Marshal.SizeOf<long>();
                        long messageSize = Shared.GetLong(Shared.ReadBytes(str, client.ReceiveBufferSize, longSize));
                        byte[] messageBytes = Shared.ReadBytes(str, client.ReceiveBufferSize, messageSize);
                        string message = Shared.GetString(messageBytes);

                        if (message.Length == 0) {
                            message = _registeredEndPoints;
                        }
                        else {
                            Interlocked.Exchange(ref _registeredEndPoints, message);
                            message = string.Empty;
                        }

                        messageBytes = Shared.GetBytes(message);
                        byte[] messageSizeBytes = Shared.GetBytes(messageBytes.LongLength);
                        byte[] bytes = new byte[messageSizeBytes.LongLength + messageBytes.LongLength];

                        long pos = 0L;
                        messageSizeBytes.CopyTo(bytes, pos);
                        pos += messageSizeBytes.LongLength;
                        messageBytes.CopyTo(bytes, pos);

                        Shared.WriteBytes(str, client.SendBufferSize, bytes);
                    }
                }
                catch (Exception ex) {
                    if (!_isDisposed)
                        eventLog.WriteEntry("Failed handeling endpoint request. " + ex, EventLogEntryType.Warning);
                }
            }, null);
        }
        /// <summary>
        /// 
        /// </summary>
        protected override void OnStop() {
            _isDisposed = true;
            if (_receiver != null) {
                try {
                    _receiver.Stop();
                }
                catch {
                    //Don't care.
                }
                _receiver = null;
            }
        }
    }
}
