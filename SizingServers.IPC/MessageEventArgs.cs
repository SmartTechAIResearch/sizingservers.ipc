/*
 * Copyright 2015 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;

namespace SizingServers.IPC {
    /// <summary>
    /// </summary>
    public class MessageEventArgs : EventArgs {
        /// <summary>
        /// </summary>
        public object Message { get; internal set; }
    }
}
