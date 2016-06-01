/*
 * Copyright 2016 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SizingServers.IPC.EndPointManagerService {
    /// <summary>
    /// Serves at storing and providing receiver end points so communication between senders and receivers can be established.
    /// </summary>
    public partial class Service : ServiceBase {
        private readonly object _lock = new object();

        private TcpListener _receiver;
        private TcpListener _receiverv6;

        private int _port;

        //Encryption of traffic. Alternatively you can use an ssh tunnel, that will probably be safer and faster.
        private string _password;
        private byte[] _salt;

        private bool _isDisposed;
        private string _registeredEndPoints = string.Empty;

        private System.Timers.Timer _cleanupTimer = new System.Timers.Timer(60000);

        /// <summary>
        /// Serves at storing and providing receiver end points so communication between senders and receivers can be established.
        /// </summary>
        public Service(string[] args) {
            InitializeComponent();
            eventLog.Source = ServiceName;
            HandleArgs(args);

            _cleanupTimer.Elapsed += _cleanupTimer_Elapsed;
            _cleanupTimer.Start();
        }

        /// <summary>
        /// For debugging purposes only.
        /// </summary>
        public void Start(string[] args) { OnStart(args); }

        private void HandleArgs(string[] args) {
            _port = Shared.EPMS_DEFAULT_TCP_PORT;
            _password = null;
            _salt = null;

            if (args == null || args.Length == 0) {
                eventLog.WriteEntry("No port in startup arguments given. Attempting to listen on the default tcp port (" + _port + ").");
            }
            else {
                if (int.TryParse(args[0], out _port)) {
                    eventLog.WriteEntry("Attempting to listen on tcp port " + _port + ".");
                }
                else {
                    _port = Shared.EPMS_DEFAULT_TCP_PORT;
                    eventLog.WriteEntry("Failed to get the tcp port in the startup arguments. The given argument cannot be parsed to an integer. Attempting to listen on the default tcp port (" + _port + ").", EventLogEntryType.Warning);
                }

                if (args.Length == 3) {
                    _salt = ConvertSalt(args[2]);
                    if (_salt != null) {
                        _password = args[1];
                        if (_password.Length == 0) {
                            _password = null;
                            _salt = null;
                        }
                        else {
                            eventLog.WriteEntry("Encrypting traffic to and from the service using the given password and salt.");
                        }
                    }
                }
            }
        }

        private byte[] ConvertSalt(string salt) {
            byte[] bytes = null;
            salt = salt.Trim();
            if (salt.StartsWith("{") && salt.EndsWith("}")) {
                salt = salt.Substring(1, salt.Length - 2);
                var split = salt.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                bytes = new byte[split.Length];
                for (int i = 0; i != bytes.Length; i++) {
                    try {
                        bytes[i] = Convert.ToByte(split[i].Trim().Substring(2), 16);
                    }
                    catch {
                        eventLog.WriteEntry("Failed parsing the salt from the startup arguments. Reverting to unencrypted traffic", EventLogEntryType.Warning);
                        return null;
                    }
                }
            }
            return bytes;
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
                        string message = MessageFromBytes(messageBytes);

                        lock (_lock)
                            if (message.Length == 0) {
                                message = _registeredEndPoints;
                            }
                            else {
                                _registeredEndPoints = message;
                                message = string.Empty;
                            }

                        messageBytes = MessageToBytes(message);
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

        private string MessageFromBytes(byte[] messageBytes) {
            string message = Shared.GetString(messageBytes);
            if (_password != null) message =  Shared.Decrypt(message, _password, _salt);
            return message;
        }
        private byte[] MessageToBytes(string message) {
            if (_password != null) message = Shared.Encrypt(message, _password, _salt);
            return Shared.GetBytes(message);
        }

        /// <summary>
        /// Cleanup end points that are not in use anymore.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _cleanupTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            if (!_isDisposed)
                lock (_lock) {
                    int equals = 1;

                    var endPoints = DeserializeEndPoints();
                    var newEndPoints = new ConcurrentDictionary<string, KeyValuePair<string, ConcurrentBag<int>>>(); ;
                    Parallel.ForEach(endPoints.Keys, (handle, loopState) => {
                        if (_isDisposed) loopState.Break();

                        var kvp = endPoints[handle];

                        IPAddress ipAddress = null;
                        foreach (var ipCandidate in Dns.GetHostEntry(kvp.Key).AddressList) {
                            if (_isDisposed) loopState.Break();
                            if (!ipCandidate.Equals(IPAddress.Loopback) && !ipCandidate.Equals(IPAddress.IPv6Loopback) &&
                                    (ipCandidate.AddressFamily == AddressFamily.InterNetwork || ipCandidate.AddressFamily == AddressFamily.InterNetworkV6)) {
                                ipAddress = ipCandidate;
                                break;
                            }
                        }

                        if (ipAddress != null) {
                            Parallel.ForEach(kvp.Value, (port, loopState2) => {
                                if (_isDisposed) loopState2.Break();

                                using (var client = new TcpClient(ipAddress.AddressFamily)) {
                                    var result = client.BeginConnect(ipAddress, port, null, null);

                                    result.AsyncWaitHandle.WaitOne(10000);
                                    if (client.Connected) {
                                        client.EndConnect(result);

                                        newEndPoints.TryAdd(handle, new KeyValuePair<string, ConcurrentBag<int>>(kvp.Key, new ConcurrentBag<int>()));
                                        newEndPoints[handle].Value.Add(port);
                                    }
                                    else {
                                        Interlocked.Exchange(ref equals, 0);
                                    }
                                }
                            });
                        }
                        else {
                            Interlocked.Exchange(ref equals, 0);
                        }
                    });

                    if (equals == 0)
                        SerializeEndPoints(newEndPoints);
                }
        }

        private ConcurrentDictionary<string, KeyValuePair<string, ConcurrentBag<int>>> DeserializeEndPoints() {
            var endPointsDic = new ConcurrentDictionary<string, KeyValuePair<string, ConcurrentBag<int>>>();

            if (_registeredEndPoints.Length != 0) {
                string[] split = _registeredEndPoints.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string token in split) {
                    string[] kvp = token.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
                    string handle = kvp[0];

                    string[] kvpConnection = kvp[1].Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    string hostname = kvpConnection[0];
                    var ports = kvpConnection[1].Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);

                    var hs = new ConcurrentBag<int>();
                    foreach (string port in ports)
                        hs.Add(int.Parse(port));

                    endPointsDic.TryAdd(handle, new KeyValuePair<string, ConcurrentBag<int>>(hostname, hs));
                }
            }

            return endPointsDic;
        }
        private void SerializeEndPoints(ConcurrentDictionary<string, KeyValuePair<string, ConcurrentBag<int>>> endPointsDic) {
            var sb = new StringBuilder();
            foreach (string handle in endPointsDic.Keys) {
                sb.Append(handle);
                sb.Append('*');

                var kvpConnection = endPointsDic[handle];
                sb.Append(kvpConnection.Key);
                sb.Append('-');

                foreach (int port in kvpConnection.Value) {
                    sb.Append(port);
                    sb.Append('+');
                }

                sb.Append(',');
            }

            _registeredEndPoints = sb.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnStop() {
            if (!_isDisposed) {
                _isDisposed = true;
                if (_cleanupTimer != null) {
                    try {
                        _cleanupTimer.Dispose();
                    }
                    catch {
                        //Don't care.
                    }
                    _cleanupTimer = null;
                }
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
}
