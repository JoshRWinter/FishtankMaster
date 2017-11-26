using System;
using System.Net.Sockets;

namespace FishtankMaster
{
    internal class Server
    {
        internal string Name { get; set; }
        internal string Location { get; set; }
        internal string IpAddress { get; set; }
        internal TcpClient Tcp { get; set; }
        internal byte Count { get; set; }
    }
}
