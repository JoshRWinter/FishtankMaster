using System;
using System.Net.Sockets;

namespace FishtankMaster
{
    class Program
    {
        static void Main(string[] args)
        {
            try{
                var fishtank = new Fishtank();
                fishtank.Exec();
            }
            catch(SocketException e)
            {
                if(e.Message.Contains("already in use"))
                    Console.WriteLine("The kernel is complaining about \"Socket already in use\".\nThis just means to wait a bit, then try again");
                else
                    Console.WriteLine(e.Message);
            }
        }
    }
}
