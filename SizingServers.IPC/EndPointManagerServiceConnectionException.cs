/*
 * Copyright 2016 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;

namespace SizingServers.IPC {
    /// <summary>
    /// Thrown when could not connect to the epms via EndPointManagerServiceConnection.GetClient();
    /// </summary>
    public class EndPointManagerServiceConnectionException : Exception {
        /// <summary>
        /// Thrown when could not connect to the epms via EndPointManagerServiceConnection.GetClient();
        /// </summary>
        /// <param name="message"></param>
        public EndPointManagerServiceConnectionException(string message) : base(message) {
        }
    }
}
