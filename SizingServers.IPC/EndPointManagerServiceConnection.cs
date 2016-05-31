/*
 * Copyright 2016 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System.Net;
using System.Net.Sockets;

namespace SizingServers.IPC {
    /// <summary>
    /// </summary>
    public class EndPointManagerServiceConnection {
        private TcpClient _client;

        /// <summary>
        /// End point info to connect to the service over tcp.
        /// </summary>
        public IPEndPoint EndPointManagerServiceEP { get; private set; }

        /// <summary>
        /// Password for Rijndael encryption for communication with the epm service.
        /// </summary>
        public string Password { get; private set; }
        /// <summary>
        /// Salt for Rijndael encryption for communication with the epm service. Example (don't use this): new byte[] { 0x59, 0x06, 0x59, 0x3e, 0x21, 0x4e, 0x55, 0x34, 0x96, 0x15, 0x11, 0x13, 0x72 }
        /// </summary>
        public byte[] Salt { get; private set; }

        /// <summary>
        /// </summary>
        /// <param name="endPointManagerServiceEP">End point info to connect to the service over tcp.</param>
        public EndPointManagerServiceConnection(IPEndPoint endPointManagerServiceEP) {
            EndPointManagerServiceEP = endPointManagerServiceEP;
        }
        /// <summary>
        /// </summary>
        /// <param name="endPointManagerServiceEP">End point info to connect to the service over tcp.</param>
        /// <param name="password">Password for Rijndael encryption for communication with the epm service.</param>
        /// <param name="salt">Salt for Rijndael encryption for communication with the epm service. Example (don't use this): new byte[] { 0x59, 0x06, 0x59, 0x3e, 0x21, 0x4e, 0x55, 0x34, 0x96, 0x15, 0x11, 0x13, 0x72 }</param>
        public EndPointManagerServiceConnection(IPEndPoint endPointManagerServiceEP, string password, byte[] salt)
            : this(endPointManagerServiceEP) {
            Password = password;
            Salt = salt;
        }

        internal TcpClient GetClient() {
            if (_client == null || !_client.Connected) {
                _client = new TcpClient(EndPointManagerServiceEP.AddressFamily);
                var result = _client.BeginConnect(EndPointManagerServiceEP.Address, EndPointManagerServiceEP.Port, null, null);

                result.AsyncWaitHandle.WaitOne(10000);

                if (!_client.Connected)
                    throw new EndPointManagerServiceConnectionException("Could not connect to the end point manager service " + EndPointManagerServiceEP.Address + ":" + EndPointManagerServiceEP.Port + ".");

                _client.EndConnect(result);
            }

            return _client;
        }
    }
}
