/*
 * Copyright 2015 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Net;
using System.Timers;

namespace SizingServers.IPC.TestSender {
    class Program {
        static Sender _sender;
        static void Main(string[] args) {
            Console.Title = "SizingServers.Message.TestSender";
            Console.WriteLine("A message is sent every second to all receivers.");

            var epmsCon = new EndPointManagerServiceConnection() { EndPointManagerServiceEP = new IPEndPoint(IPAddress.Loopback, 4455) };

            _sender = new Sender("SizingServers.IPC.Test", epmsCon);
            _sender.AfterMessageSent += _sender_AfterMessageSent;

            var tmr = new Timer(1000);
            tmr.Elapsed += Tmr_Elapsed;
            tmr.Start();

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();
            tmr.Stop();
            _sender.Dispose();
        }

        private static void _sender_AfterMessageSent(object sender, MessageEventArgs e) {
            object message = e.Message;
            if (message is byte[]) message = System.Text.Encoding.UTF8.GetString(message as byte[]);
            Console.WriteLine("'" + message + "'sent");
        }

        private static void Tmr_Elapsed(object sender, ElapsedEventArgs e) {
            //(sender as Timer).Stop();
            _sender.Send("Foo");
            //_sender.Send(System.Text.Encoding.UTF8.GetBytes("FooBytes"));
        }
    }
}
