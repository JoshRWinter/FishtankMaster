using System;
using System.Net.Sockets;
using System.Threading;

namespace FishtankMaster
{
    class Program
    {
        private static volatile bool running = true;

        static void Main(string[] args)
        {
            try{
                var fishtank = new Fishtank();

                // register CTRL-C handler
                Console.CancelKeyPress += new ConsoleCancelEventHandler(Handler);

                Console.WriteLine("[ready on tcp:28860 udp:28860]");

                while(running)
                {
                    fishtank.Exec();
                    Thread.Sleep(210);
                }

                fishtank.Close();
                Console.Write("\nexiting...");

                return;
            }
            catch(SocketException e)
            {
                if(e.Message.Contains("already in use"))
                    Console.WriteLine("The kernel is complaining about \"Socket already in use\".\nThis just means to wait a bit, then try again");
                else
                    Console.WriteLine(e.Message);

                Environment.ExitCode = 1;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);

                Main(args);
            }
        }

        private static void Handler(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            running = false;
        }
    }
}
