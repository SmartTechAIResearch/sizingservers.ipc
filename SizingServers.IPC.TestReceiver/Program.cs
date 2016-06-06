/*
 * Copyright 2015 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Net;

namespace SizingServers.IPC.TestReceiver {
    class Program {
        static Receiver _receiver;
        static void Main(string[] args) {
            Console.Title = "SizingServers.Message.TestReceiver";
            Console.WriteLine("Messages are received from TestSender.");
            
            //var epmsCon = new EndPointManagerServiceConnection(new IPEndPoint(IPAddress.Loopback, Shared.EPMS_DEFAULT_TCP_PORT));
            var epmsCon = new EndPointManagerServiceConnection(new IPEndPoint(IPAddress.Loopback, Shared.EPMS_DEFAULT_TCP_PORT), "password", new byte[] { 0x01, 0x02, 0x03 });

            //_receiver = new Receiver("SizingServers.IPC.Test");
            _receiver = new Receiver("SizingServers.IPC.Test", null, epmsCon);
            _receiver.MessageReceived += _receiver_MessageReceived;

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();
            _receiver.Dispose();
        }

        private static void _receiver_MessageReceived(object sender, MessageEventArgs e) {
            object message = e.Message;
            if (message is byte[]) message = System.Text.Encoding.UTF8.GetString(message as byte[]);
            Console.WriteLine("'" + message + "' received");
        }
    }
}
