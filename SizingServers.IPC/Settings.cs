using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SizingServers.IPC {
    /// <summary>
    /// 
    /// </summary>
    public class Settings {
        private TcpClient _client;

        /// <summary>
        /// 
        /// </summary>
        public string EndPointManagerServiceIP { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int EndpointManagerServicePort { get; set; }

        internal TcpClient GetClient() {
            if (_client == null || !_client.Connected) 
                _client = new TcpClient(new IPEndPoint(IPAddress.Parse(EndPointManagerServiceIP), EndpointManagerServicePort));
            return _client;
        }
    }
}
