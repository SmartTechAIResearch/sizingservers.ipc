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
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
        public const string KEY = "SizingServers.IPC{8B20C7BD-634B-408D-B337-732644177389}";

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
        /// <param name="endPointManagerServiceConnection"></param>
        /// <returns></returns>
        public static IPEndPoint RegisterReceiver(string handle, EndPointManagerServiceConnection endPointManagerServiceConnection) {
            if (string.IsNullOrWhiteSpace(handle)) throw new ArgumentNullException(handle);

            IPEndPoint endPoint = null;

            IPAddress ipAddress = null;
            foreach (var ipCandidate in Dns.GetHostEntry(IPAddress.Loopback).AddressList)
                if (!ipCandidate.Equals(IPAddress.Loopback) && !ipCandidate.Equals(IPAddress.IPv6Loopback) &&
                    (ipCandidate.AddressFamily == AddressFamily.InterNetwork || ipCandidate.AddressFamily == AddressFamily.InterNetworkV6)) {
                    ipAddress = ipCandidate;
                    break;
                }

            var endPoints = GetRegisteredEndPoints(endPointManagerServiceConnection);
            if (endPointManagerServiceConnection == null) CleanupEndPoints(endPoints, false);
            if (!endPoints.ContainsKey(handle)) endPoints.Add(handle, new KeyValuePair<string, HashSet<int>>(ipAddress.ToString(), new HashSet<int>()));

            endPoint = new IPEndPoint(ipAddress, GetAvailableTcpPort());
            endPoints[handle].Value.Add(endPoint.Port);
            SetRegisteredEndPoints(endPoints, endPointManagerServiceConnection);

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
        /// <param name="endPointManagerServiceConnection"></param>
        /// <returns></returns>
        public static List<IPEndPoint> GetReceiverEndPoints(string handle, EndPointManagerServiceConnection endPointManagerServiceConnection) {
            var endPoints = new List<IPEndPoint>();

            var allEndPoints = GetRegisteredEndPoints(endPointManagerServiceConnection);
            if (endPointManagerServiceConnection == null) CleanupEndPoints(allEndPoints, true);

            if (allEndPoints.ContainsKey(handle)) {
                var kvpConnection = allEndPoints[handle];
                var ipAddress = IPAddress.Parse(kvpConnection.Key);
                foreach (int port in kvpConnection.Value)
                    endPoints.Add(new IPEndPoint(ipAddress, port));
            }

            return endPoints;
        }

        /// <summary>
        /// </summary>
        /// <param name="endPointManagerServiceConnection"></param>
        /// <returns></returns>
        private static Dictionary<string, KeyValuePair<string, HashSet<int>>> GetRegisteredEndPoints(EndPointManagerServiceConnection endPointManagerServiceConnection) {
            var endPoints = new Dictionary<string, KeyValuePair<string, HashSet<int>>>();

            string value = endPointManagerServiceConnection == null ? GetRegisteredEndPoints() : endPointManagerServiceConnection.SendAndReceiveEPM(string.Empty);
            if (value.Length != 0) {
                string[] split = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string token in split) {
                    string[] kvp = token.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
                    string handle = kvp[0];

                    string[] kvpConnection = kvp[1].Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    string hostname = kvpConnection[0];
                    var ports = kvpConnection[1].Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);

                    var hs = new HashSet<int>();
                    foreach (string port in ports)
                        hs.Add(int.Parse(port));

                    endPoints.Add(handle, new KeyValuePair<string, HashSet<int>>(hostname, hs));
                }
            }

            if (endPointManagerServiceConnection == null) CleanupEndPoints(endPoints, true);

            return endPoints;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static string GetRegisteredEndPoints() {
            string endPoints = string.Empty;
            if (_namedMutex.WaitOne()) {
                RegistryKey subKey = Registry.CurrentUser.OpenSubKey("Software\\" + EndPointManager.KEY);
                if (subKey != null)
                    endPoints = subKey.GetValue("EndPoints") as string;

                _namedMutex.ReleaseMutex();
            }
            return endPoints;
        }

        /// <summary>
        /// Only used for local IPC. When using the end point manager service, this service handles the cleaning.
        /// </summary>
        /// <param name="endPoints">All end points that are not used anymore are filtered out.</param>
        /// <param name="registerEndpoints">Register the cleaned end point is applicable.</param>
        private static void CleanupEndPoints(Dictionary<string, KeyValuePair<string, HashSet<int>>> endPoints, bool registerEndpoints) {
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
            if (registerEndpoints && !equals) {
                endPoints = newEndPoints;
                SetRegisteredEndPoints(endPoints, null);
            }
        }

        /// <summary>
        /// Set endpoints to the registery.
        /// </summary>
        /// <param name="endPoints"></param>
        /// <param name="endPointManagerServiceConnection"></param>
        private static void SetRegisteredEndPoints(Dictionary<string, KeyValuePair<string, HashSet<int>>> endPoints, EndPointManagerServiceConnection endPointManagerServiceConnection) {
            var sb = new StringBuilder();
            foreach (string handle in endPoints.Keys) {
                sb.Append(handle);
                sb.Append('*');

                var kvpConnection = endPoints[handle];
                sb.Append(kvpConnection.Key);
                sb.Append('-');

                foreach (int port in kvpConnection.Value) {
                    sb.Append(port);
                    sb.Append('+');
                }

                sb.Append(',');
            }

            if (endPointManagerServiceConnection == null)
                SetRegisteredEndPoints(sb.ToString());
            else
                endPointManagerServiceConnection.SendAndReceiveEPM(sb.ToString());
        }
        /// <summary>
        /// Set endpoints to the registery.
        /// </summary>
        /// <param name="endPoints"></param>
        private static void SetRegisteredEndPoints(string endPoints) {
            if (_namedMutex.WaitOne()) {
                RegistryKey subKey = Registry.CurrentUser.CreateSubKey("Software\\" + EndPointManager.KEY, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.Volatile);
                subKey.SetValue("Endpoints", endPoints, RegistryValueKind.String);

                _namedMutex.ReleaseMutex();
            }
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
