/*
 * Copyright 2016 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System.ServiceProcess;

namespace SizingServers.IPC.EndPointManagerService {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args) {
            //(new Service(args)).Start();
            //System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);

            ServiceBase.Run(new Service(args));
        }
    }
}
