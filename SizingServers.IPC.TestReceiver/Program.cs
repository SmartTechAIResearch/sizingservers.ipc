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

            var epmsCon = new EndPointManagerServiceConnection() { EndPointManagerServiceEP = new IPEndPoint(IPAddress.Parse("192.168.2.34"), 4455) };

            _receiver = new Receiver("SizingServers.IPC.Test", epmsCon);
            _receiver.MessageReceived += _receiver_MessageReceived;

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();
            _receiver.Dispose();
        }

        private static void _receiver_MessageReceived(object sender, MessageEventArgs e) {
            object message = e.Message;
            if (message is byte[]) message =System.Text.Encoding.UTF8.GetString(message as byte[]);
            Console.WriteLine("'" + message + "' received");
        }
    }
}
