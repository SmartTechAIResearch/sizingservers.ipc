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
        static int _count;
        static void Main(string[] args) {
            Console.Title = "SizingServers.Message.TestSender";
            Console.WriteLine("A message is sent every second to all receivers.");

            //var epmsCon = new EndPointManagerServiceConnection(new IPEndPoint(IPAddress.Loopback, Shared.EPMS_DEFAULT_TCP_PORT));
            var epmsCon = new EndPointManagerServiceConnection(new IPEndPoint(IPAddress.Loopback, Shared.EPMS_DEFAULT_TCP_PORT), "password", new byte[] { 0x01, 0x02, 0x03 });

            //_sender = new Sender("SizingServers.IPC.Test");
            _sender = new Sender("SizingServers.IPC.Test", epmsCon);
            _sender.AfterMessageSent += _sender_AfterMessageSent;
            _sender.OnSendFailed += _sender_OnSendFailed;

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
            string s = "'" + message + "' sent";
            if (e.RemoteEndPoints.Length == 0) s += ", but no receivers found";
            Console.WriteLine(s);
        }
        private static void _sender_OnSendFailed(object sender, System.IO.ErrorEventArgs e) {
            Console.WriteLine(e.GetException());
        }

        private static void Tmr_Elapsed(object sender, ElapsedEventArgs e) {
            //(sender as Timer).Stop();
            _sender.Send("Foo" + (++_count));
            //_sender.Send(System.Text.Encoding.UTF8.GetBytes("FooBytes"));
        }
    }
}
