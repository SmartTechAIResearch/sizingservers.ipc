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
    /// Stores end points (handles and tcp ports) in the Windows Registry for the current user or in an end point manager service for the IPC message receivers.
    /// </summary>
    internal static class EndPointManager {
        /// <summary>
        /// Used for a key in the Windows Registry (current user, volatile) to store the end points when not using an end point manager service.
        /// </summary>
        public const string WINDOWS_REGISTRY_KEY = "SizingServers.IPC{8B20C7BD-634B-408D-B337-732644177389}";

        private static Mutex _namedMutex = new Mutex(false, WINDOWS_REGISTRY_KEY);

        /// <summary>
        /// Add a new tcp port to the endpoints for a receiver.
        /// </summary>
        /// <param name="handle">
        /// <para>The handle is a value shared by a Sender and its Receivers.  ; , * + and - cannot be used!</para>
        /// <para>It links both parties so messages from a Sender get to the right Receivers.</para>
        /// <para>Make sure this is a unique value: use a GUID for instance:</para>
        /// <para>There is absolutely no checking to see if this handle is used in another Sender - Receivers relation.</para>
        /// </param>
        /// <param name="ipAddressToRegister">
        /// <para>This parameter is only useful if you are using an end point manager service.</para>
        /// <para>A receiver listens to all available IPs for connections. The ip that is registered on the end point manager (service) is by default automatically determined.</para>
        /// <para>However, this does not take into account that senders, receiver or end point manager services are possibly not on the same network.</para>
        /// <para>Therefor you can override this behaviour by supplying your own IP that will be registered to the end point manager service.</para>
        /// </param>
        /// <param name="allowedPorts">
        /// <para>This parameter is only useful if you are using an end point manager service.</para>
        /// <para>To make firewall settings easier, you can specify a pool of TCP ports where the receiver can choose one from to listen on. If none of these ports are available, this will fail.</para>
        /// <para>If you don't use this parameter, one of the total available ports on the system will be chosen.</para>
        /// </param>
        /// <param name="endPointManagerServiceConnection">
        /// <para>This is an optional parameter.</para>
        /// <para>If you don't use it, receiver end points are stored in the Windows registry and IPC communication is only possible for processes running under the current local user.</para>
        /// <para>If you do use it, these end points are fetched from a Windows service over tcp, making it a distributed IPC.This however will be slower and implies a security risk since there will be network traffic.</para>
        /// </param>
        /// <returns></returns>
        internal static IPEndPoint RegisterReceiver(string handle, IPAddress ipAddressToRegister, int[] allowedPorts, EndPointManagerServiceConnection endPointManagerServiceConnection) {
            if (string.IsNullOrWhiteSpace(handle)) throw new ArgumentNullException(handle);

            IPEndPoint endPoint = null;

            if (ipAddressToRegister == null)
                foreach (var ipCandidate in Shared.GetIPs())
                    if (!ipCandidate.Equals(IPAddress.Loopback) && !ipCandidate.Equals(IPAddress.IPv6Loopback) &&
                        (ipCandidate.AddressFamily == AddressFamily.InterNetwork || ipCandidate.AddressFamily == AddressFamily.InterNetworkV6)) {
                        ipAddressToRegister = ipCandidate;
                        break;
                    }

            string ip = ipAddressToRegister.ToString();

            var endPoints = GetRegisteredEndPoints(endPointManagerServiceConnection);
            if (endPointManagerServiceConnection == null) CleanupEndPoints(endPoints, false);
            if (!endPoints.ContainsKey(handle)) endPoints.Add(handle, new Dictionary<string, HashSet<int>>());
            if (!endPoints[handle].ContainsKey(ip)) endPoints[handle].Add(ip, new HashSet<int>());

            endPoint = new IPEndPoint(ipAddressToRegister, GetAvailableTcpPort(allowedPorts));
            endPoints[handle][ip].Add(endPoint.Port);

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
        internal static List<IPEndPoint> GetReceiverEndPoints(string handle, EndPointManagerServiceConnection endPointManagerServiceConnection) {
            var endPoints = new List<IPEndPoint>();

            var allEndPoints = GetRegisteredEndPoints(endPointManagerServiceConnection);

            //Only cleanup if there is no epm service.
            if (endPointManagerServiceConnection == null) CleanupEndPoints(allEndPoints, true);

            if (allEndPoints.ContainsKey(handle)) {
                var dic = allEndPoints[handle];
                foreach (string ip in dic.Keys) {
                    var ipAddress = IPAddress.Parse(ip);
                    foreach (int port in dic[ip])
                        endPoints.Add(new IPEndPoint(ipAddress, port));
                }
            }

            return endPoints;
        }

        /// <summary>
        /// </summary>
        /// <param name="endPointManagerServiceConnection"></param>
        /// <returns></returns>
        private static Dictionary<string, Dictionary<string, HashSet<int>>> GetRegisteredEndPoints(EndPointManagerServiceConnection endPointManagerServiceConnection) {
            var endPoints = new Dictionary<string, Dictionary<string, HashSet<int>>>();

            string value = endPointManagerServiceConnection == null ? GetRegisteredEndPoints() : endPointManagerServiceConnection.SendAndReceiveEPM(string.Empty);
            if (value.Length != 0) {
                string[] handleKvps = value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string token1 in handleKvps) {
                    string[] handleKvp = token1.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
                    string handle = handleKvp[0];

                    string[] ipKvps = handleKvp[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string token2 in ipKvps) {
                        string[] ipKvp = token2.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                        string ip = ipKvp[0];
                        var ports = ipKvp[1].Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);

                        if (!endPoints.ContainsKey(handle)) endPoints.Add(handle, new Dictionary<string, HashSet<int>>());
                        if (!endPoints[handle].ContainsKey(ip)) endPoints[handle].Add(ip, new HashSet<int>());

                        var hs = endPoints[handle][ip];
                        foreach (string port in ports)
                            hs.Add(int.Parse(port));
                    }
                }
            }

            //The service, if any, handles the cleaning. Otherwise this must be done here.
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
                RegistryKey subKey = Registry.CurrentUser.OpenSubKey("Software\\" + WINDOWS_REGISTRY_KEY);
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
        private static void CleanupEndPoints(Dictionary<string, Dictionary<string, HashSet<int>>> endPoints, bool registerEndpoints) {
            HashSet<int> usedPorts = GetUsedTcpPorts();

            bool equals = true;
            var newEndPoints = new Dictionary<string, Dictionary<string, HashSet<int>>>();
            foreach (string handle in endPoints.Keys) {
                var dic = endPoints[handle];
                foreach (string ip in dic.Keys)
                    foreach (int port in dic[ip]) {
                        if (usedPorts.Contains(port)) {
                            if (!newEndPoints.ContainsKey(handle)) newEndPoints.Add(handle, new Dictionary<string, HashSet<int>>());
                            if (!newEndPoints[handle].ContainsKey(ip)) newEndPoints[handle].Add(ip, new HashSet<int>());

                            newEndPoints[handle][ip].Add(port);
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
        /// Set endpoints to the Windows Registry (current user, volatile) or the end point manager service, if any.
        /// </summary>
        /// <param name="endPoints"></param>
        /// <param name="endPointManagerServiceConnection"></param>
        private static void SetRegisteredEndPoints(Dictionary<string, Dictionary<string, HashSet<int>>> endPoints, EndPointManagerServiceConnection endPointManagerServiceConnection) {
            var sb = new StringBuilder();
            foreach (string handle in endPoints.Keys) {
                sb.Append(handle);
                sb.Append('*');

                var ips = endPoints[handle];
                foreach (string ip in ips.Keys) {
                    sb.Append(ip);
                    sb.Append('-');

                    foreach (int port in ips[ip]) {
                        sb.Append(port);
                        sb.Append('+');
                    }

                    sb.Append(',');
                }
                sb.Append(';');
            }

            if (endPointManagerServiceConnection == null)
                SetRegisteredEndPoints(sb.ToString());
            else
                endPointManagerServiceConnection.SendAndReceiveEPM(sb.ToString());
        }
        /// <summary>
        /// Set endpoints to the Windows Registry (current user, volatile).
        /// </summary>
        /// <param name="endPoints"></param>
        private static void SetRegisteredEndPoints(string endPoints) {
            if (_namedMutex.WaitOne()) {
                RegistryKey subKey = Registry.CurrentUser.CreateSubKey("Software\\" + WINDOWS_REGISTRY_KEY, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.Volatile);
                subKey.SetValue("EndPoints", endPoints, RegistryValueKind.String);

                _namedMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static int GetAvailableTcpPort(int[] allowedPorts) {
            HashSet<int> usedPorts = GetUsedTcpPorts();
            if (allowedPorts == null)
                for (int port = 1; port != int.MaxValue; port++) //0 == random port.
                    if (!usedPorts.Contains(port)) return port;

            foreach (int port in allowedPorts)
                if (!usedPorts.Contains(port)) return port;

            return -1;
        }
        /// <summary>
        /// Only take used tcp ports into accounts. What's been registered in the registry or the epm service does not matter.
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
