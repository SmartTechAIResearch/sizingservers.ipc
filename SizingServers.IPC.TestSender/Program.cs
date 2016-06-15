/*
Original author: Dieter Vandroemme, dev at Sizing Servers Lab (https://www.sizingservers.be) @ University College of West-Flanders, Department GKG
Written in 2015

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
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

            var epmsCon = new EndPointManagerServiceConnection(new IPEndPoint(IPAddress.Loopback, Shared.EPMS_DEFAULT_TCP_PORT));
            //var epmsCon = new EndPointManagerServiceConnection(new IPEndPoint(IPAddress.Loopback, Shared.EPMS_DEFAULT_TCP_PORT), "password", new byte[] { 0x01, 0x02, 0x03 });

            _sender = new Sender("SizingServers.IPC.Test");
            //_sender = new Sender("SizingServers.IPC.Test", epmsCon);
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
