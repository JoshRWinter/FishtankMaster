using System;
using System.Collections.Generic;

namespace FishtankMaster
{
    internal class Registry
    {
        private List<Server> servers = new List<Server>();
        private string error;

        // clone the internal list and return it
        internal List<Server> Get()
        {
            List<Server> copy = new List<Server>();

            lock (servers)
            {
                foreach (var server in servers)
                {
                    copy.Add(server.Copy());
                }
            }

            return copy;
        }

        internal void Sort()
        {
            lock (servers)
            {
                servers.Sort();
            }
        }

        internal bool Add(Server server)
        {
            if (!Valid(server))
            {
                return false;
            }

            lock (servers)
            {
                servers.Add(server.Copy());
            }

            Console.WriteLine($"Registered Name: {server.Name}, Location: {server.Location}, IpAddress: {server.IpAddress}");

            return true;
        }

        internal bool Remove(string name)
        {
            lock (servers)
            {
                for (int i = 0; i < servers.Count; ++i)
                {
                    if (servers[i].Name == name)
                    {
                        servers.Remove(servers[i]);
                        return true;
                    }
                }

                error = "Unable to remove server \"" + name + "\" because it does not exist in the registry!";
                return false;
            }
        }

        internal bool Update(string ip, byte pc)
        {
            lock (servers)
            {
                for (int i = 0; i < servers.Count; ++i)
                {
                    if (servers[i].IpAddress == ip)
                    {
                        servers[i].Count = pc;
                        servers[i].LastHeartbeat = Fishtank.UnixTime();
                        return true;
                    }
                }
            }

            error = "No server with ip address: \"" + ip + "\" exists in the registry";
            return false;
        }

        internal string Error()
        {
            return string.Copy(error);
        }

        private bool Valid(Server server)
        {
            lock (servers)
            {
                foreach (var s in servers)
                {
                    if (s.Name == server.Name)
                    {
                        error = "There is already a server registered with name \"" + server.Name + "\"";
                        return false;
                    }
                    else if (s.IpAddress == server.IpAddress)
                    {
                        error = "There is already a server registered with ip address \"" + server.IpAddress + "\"";
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
