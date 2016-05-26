using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace SizingServers.IPC.EndPointManagerService {
    public partial class Service : ServiceBase {
        private TcpListener _receiver;
        private int _port = 4444;
        private bool _isDisposed;

        public Service() {
            InitializeComponent();
        }

        public void Start() {
            OnStart(null);
        }

        protected override void OnStart(string[] args) {
            eventLog.Source = ServiceName;
            if (args == null || args.Length == 0) {
                eventLog.WriteEntry("No port in startup arguments given. Attempting to listen on the default tcp port (" + _port + ").");
            }
            else {
                if (int.TryParse(args[0], out _port))
                    eventLog.WriteEntry("Attempting to listen on tcp port " + _port + ".");
                else
                    eventLog.WriteEntry("Failed to get the tcp port in the startup arguments. The given argument cannot be parsed to an integer. Attempting to listen on the default tcp port (" + _port + ").", EventLogEntryType.Warning);
            }

            try {
                _receiver = new TcpListener(IPAddress.Any, _port);
                _receiver.Start();
                eventLog.WriteEntry("Listen on tcp port " + _port + ".");
            }
            catch (Exception ex) {
                eventLog.WriteEntry("Failed to listen on tcp port " + _port + ". " + ex, EventLogEntryType.Error);
            }

            BeginReceive();
        }

        private void BeginReceive() {
            ThreadPool.QueueUserWorkItem((state) => {
                while (!_isDisposed)
                    try {
                        HandleReceive(_receiver.AcceptTcpClient());
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
                            message = Shared.GetRegisteredEndPoints();
                        }
                        else {
                            Shared.SetRegisteredEndPoints(message);
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
                        eventLog.WriteEntry("Failed handeling endpoint request. " + ex, EventLogEntryType.Error);
                }
            }, null);
        }

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
