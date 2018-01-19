using System;
using System.Net.Sockets;

namespace FishtankMaster
{
    internal class Server : IComparable
    {
        internal string Name { get; set; }
        internal string Location { get; set; }
        internal string IpAddress { get; set; }
        internal byte Count { get; set; }
        internal Int64 LastHeartbeat { get; set; }

        public int CompareTo(Object o)
        {
            Server rhs = (Server)o;

            return rhs.Count - Count;
        }
    }
}
