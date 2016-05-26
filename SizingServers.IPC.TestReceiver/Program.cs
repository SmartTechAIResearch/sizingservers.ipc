using System;

namespace SizingServers.IPC.TestReceiver {
    class Program {
        static Receiver _receiver;
        static void Main(string[] args) {
            Console.Title = "SizingServers.Message.TestReceiver";
            Console.WriteLine("Messages are received from TestSender.");

            var settings = new Settings() { EndPointManagerServiceIP = "127.0.0.1", EndpointManagerServicePort = 4444 };

            _receiver = new Receiver("SizingServers.IPC.Test", settings);
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
