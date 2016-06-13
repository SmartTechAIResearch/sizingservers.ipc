/*
Original author: Dieter Vandroemme, dev at Sizing Servers Lab (https://www.sizingservers.be) @ University College of West-Flanders, Department GKG
Written in 2015

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
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

            _receiver = new Receiver("SizingServers.IPC.Test");
            //_receiver = new Receiver("SizingServers.IPC.Test", null, null, epmsCon);
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
