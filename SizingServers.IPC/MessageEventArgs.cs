/*
 * Copyright 2015 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Net;

namespace SizingServers.IPC {
    /// <summary>
    /// </summary>
    public class MessageEventArgs : EventArgs {
        /// <summary>
        /// </summary>
        public string Handle { get; internal set; }
        /// <summary>
        /// The sent or received message.
        /// </summary>
        public object Message { get; internal set; }
        /// <summary>
        /// The local end points of the receivers. Only filled in for Sender events: before message sent - all registred receivers; After message sent - Alle responding receivers.
        /// </summary>
        public EndPoint[] RemoteEndPoints { get; internal set; }
    }
}
