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
        public IPEndPoint EndPointManagerServiceEP { get; set; }

        internal TcpClient GetClient() {
            if (_client == null || !_client.Connected) {
                _client = new TcpClient(EndPointManagerServiceEP.AddressFamily);
                _client.Connect(EndPointManagerServiceEP);
            }

            return _client;
        }
    }
}
