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
using System.Text;
using System.Threading;

namespace SizingServers.IPC {
    internal static class EndPointManager {
        private static Mutex _namedMutex = new Mutex(false, KEY);

        /// <summary>
        /// Used for a key in the registry to store the end points.
        /// </summary>
        public const string KEY = "RandomUtils.Message{8B20C7BD-634B-408D-B337-732644177389}";

        /// <summary>
        /// Add a new port tot the endpoint for the receiver.
        /// </summary>
        /// <param name="handle">
        /// <para>The handle is a value shared by a Sender and its Receivers.</para>
        /// <para>It links both parties so messages from a Sender get to the right Receivers.</para>
        /// <para>Make sure this is a unique value: use a GUID for instance:</para>
        /// <para>There is absolutely no checking to see if this handle is used in another Sender - Receivers relation.</para>
        /// </param>
        /// <returns></returns>
        public static IPEndPoint RegisterReceiver(string handle) {
            if (string.IsNullOrWhiteSpace(handle)) throw new ArgumentNullException(handle);

            IPEndPoint endPoint = null;

            if (_namedMutex.WaitOne()) {
                var endPoints = GetRegisteredEndPoints();
                if (!endPoints.ContainsKey(handle)) endPoints.Add(handle, new HashSet<int>());

                endPoint = new IPEndPoint(IPAddress.Loopback, GetAvailableTcpPort());
                endPoints[handle].Add(endPoint.Port);
                SetRegisteredEndPoints(endPoints);

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
        /// <returns></returns>
        public static List<IPEndPoint> GetReceiverEndPoints(string handle) {
            var endPoints = new List<IPEndPoint>();

            if (_namedMutex.WaitOne()) {
                var allEndPoints = GetRegisteredEndPoints();

                if (allEndPoints.ContainsKey(handle))
                    foreach (int port in allEndPoints[handle])
                        endPoints.Add(new IPEndPoint(IPAddress.Loopback, port));

                _namedMutex.ReleaseMutex();
            }

            return endPoints;
        }

        private static Dictionary<string, HashSet<int>> GetRegisteredEndPoints() {
            //Get the end points from the registry.
            var endPoints = new Dictionary<string, HashSet<int>>();

            RegistryKey subKey = Registry.CurrentUser.OpenSubKey("Software\\" + KEY);
            if (subKey != null) {
                string value = subKey.GetValue("EndPoints") as string;
                if (value != null) {
                    string[] split = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string token in split) {
                        var kvp = token.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        string id = kvp[0];

                        var ports = kvp[1].Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);

                        var hs = new HashSet<int>();
                        foreach (string port in ports)
                            hs.Add(int.Parse(port));

                        endPoints.Add(id, hs);
                    }
                }
            }

            //Cleanup end points if needed.
            HashSet<int> usedPorts = GetUsedTcpPorts();

            bool equals = true;
            var newEndPoints = new Dictionary<string, HashSet<int>>();
            foreach (string handle in endPoints.Keys)
                foreach (int port in endPoints[handle]) {
                    if (usedPorts.Contains(port)) {
                        if (!newEndPoints.ContainsKey(handle)) newEndPoints.Add(handle, new HashSet<int>());
                        newEndPoints[handle].Add(port);
                    } else {
                        equals = false;
                    }
                }
            if (!equals) {
                endPoints = newEndPoints;
                SetRegisteredEndPoints(endPoints);
            }

            return endPoints;
        }

        private static void SetRegisteredEndPoints(Dictionary<string, HashSet<int>> endPoints) {
            var sb = new StringBuilder();
            foreach (string id in endPoints.Keys) {
                sb.Append(id);
                sb.Append(':');

                foreach (int port in endPoints[id]) {
                    sb.Append(port);
                    sb.Append('+');
                }

                sb.Append(',');
            }

            RegistryKey subKey = Registry.CurrentUser.CreateSubKey("Software\\" + KEY, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.Volatile);
            subKey.SetValue("Endpoints", sb.ToString(), RegistryValueKind.String);
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

        private static int GetAvailableTcpPort() {
            HashSet<int> usedPorts = GetUsedTcpPorts();
            for (int port = 1; port != int.MaxValue; port++) //0 == random port.
                if (!usedPorts.Contains(port)) return port;
            return -1;
        }
    }
}
