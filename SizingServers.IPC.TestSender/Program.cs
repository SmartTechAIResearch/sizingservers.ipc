using System;
using System.Timers;

namespace SizingServers.IPC.TestSender {
    class Program {
        static Sender _sender = new Sender("SizingServers.IPC.Test");
        static void Main(string[] args) {
            Console.Title = "SizingServers.Message.TestSender";
            Console.WriteLine("A message is sent every second to all receivers.");

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
            _sender.Send("Foo");
            //_sender.Send(System.Text.Encoding.UTF8.GetBytes("FooBytes"));
        }
    }
}
