/*
 * Copyright 2016 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
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
