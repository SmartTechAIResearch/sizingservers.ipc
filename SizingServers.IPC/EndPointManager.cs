/*
 * Copyright 2015 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SizingServers.IPC {
    /// <summary>
    /// Stores end points (handles and tcp ports) in the registry for the IPC message receivers.
    /// </summary>
    internal static class EndPointManager {
        /// <summary>
        /// Used for a key in the registry to store the end points.
        /// </summary>
        public const string KEY = "RandomUtils.Message{8B20C7BD-634B-408D-B337-732644177389}";

        private static Mutex _namedMutex = new Mutex(false, KEY);

        /// <summary>
        /// Add a new port tot the endpoint for the receiver.
        /// </summary>
        /// <param name="handle">
        /// <para>The handle is a value shared by a Sender and its Receivers.</para>
        /// <para>It links both parties so messages from a Sender get to the right Receivers.</para>
        /// <para>Make sure this is a unique value: use a GUID for instance:</para>
        /// <para>There is absolutely no checking to see if this handle is used in another Sender - Receivers relation.</para>
        /// </param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static IPEndPoint RegisterReceiver(string handle, Settings settings) {
            if (string.IsNullOrWhiteSpace(handle)) throw new ArgumentNullException(handle);

            IPEndPoint endPoint = null;

            string hostName = Dns.GetHostEntry(IPAddress.Loopback).HostName.Trim();

            if (_namedMutex.WaitOne()) {
                var endPoints = CleanupEndPoints(Shared.GetRegisteredEndPoints(settings), settings);
                if (!endPoints.ContainsKey(handle)) endPoints.Add(handle, new KeyValuePair<string, HashSet<int>>(hostName, new HashSet<int>()));

                endPoint = new IPEndPoint(IPAddress.Any, GetAvailableTcpPort());
                endPoints[handle].Value.Add(endPoint.Port);
                Shared.SetRegisteredEndPoints(endPoints, settings);

                _namedMutex.ReleaseMutex();
            }

            return endPoint;
        }

        /// <summary>
        /// The sender must use this to be able to send data to the correct receivers.
        /// </summary>
        /// <param name="handle">
        /// <para>The handle is a value shared by a Sender and its Receivers.</para>
        /// <para>It links both parties so messages from a Sender get to the right Receivers.</para>
        /// <para>Make sure this is a unique value: use a GUID for instance:</para>
        /// <para>There is absolutely no checking to see if this handle is used in another Sender - Receivers relation.</para>
        /// </param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static List<IPEndPoint> GetReceiverEndPoints(string handle, Settings settings) {
            var endPoints = new List<IPEndPoint>();

            if (_namedMutex.WaitOne()) {
                var allEndPoints = CleanupEndPoints(Shared.GetRegisteredEndPoints(settings), settings);

                if (allEndPoints.ContainsKey(handle)) {
                    var kvpConnection = allEndPoints[handle];
                    IPAddress ipAddress = null;
                    foreach (var candidateIp in Dns.GetHostEntry(kvpConnection.Key).AddressList) {
                        if (candidateIp.AddressFamily == AddressFamily.InterNetwork) {
                            ipAddress = candidateIp;
                            break;
                        }
                    }
                    foreach (int port in kvpConnection.Value)
                        endPoints.Add(new IPEndPoint(ipAddress, port)); 
                }

                _namedMutex.ReleaseMutex();
            }

            return endPoints;
        }


        /// <summary>
        /// 
        /// </summary>
        private static Dictionary<string, KeyValuePair<string, HashSet<int>>> CleanupEndPoints(Dictionary<string, KeyValuePair<string, HashSet<int>>> endPoints, Settings settings) {
            HashSet<int> usedPorts = GetUsedTcpPorts();

            bool equals = true;
            var newEndPoints = new Dictionary<string, KeyValuePair<string, HashSet<int>>>();
            foreach (string handle in endPoints.Keys) {
                var kvpConnection = endPoints[handle];

                foreach (int port in kvpConnection.Value) {
                    if (usedPorts.Contains(port)) {
                        if (!newEndPoints.ContainsKey(handle)) 
                            newEndPoints.Add(handle, new KeyValuePair<string, HashSet<int>>(kvpConnection.Key, new HashSet<int>()));
                        
                        newEndPoints[handle].Value.Add(port);
                    }
                    else {
                        equals = false;
                    }
                }
            }
            if (!equals) {
                endPoints = newEndPoints;
                Shared.SetRegisteredEndPoints(endPoints, settings);
            }
            return endPoints;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static int GetAvailableTcpPort() {
            HashSet<int> usedPorts = GetUsedTcpPorts();
            for (int port = 1; port != int.MaxValue; port++) //0 == random port.
                if (!usedPorts.Contains(port)) return port;
            return -1;
        }
        /// <summary>
        /// Only take used tcp ports into accounts. What's been registered in the registry does not matter.
        /// </summary>
        /// <returns></returns>
        private static HashSet<int> GetUsedTcpPorts() {
            var usedPorts = new HashSet<int>();

            foreach (var info in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()) usedPorts.Add(info.Port);
            foreach (var info in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections()) usedPorts.Add(info.LocalEndPoint.Port);

            return usedPorts;
        }

    }
}
