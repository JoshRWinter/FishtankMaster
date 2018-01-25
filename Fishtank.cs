using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text;

namespace FishtankMaster
{
    internal class Fishtank
    {
        private Mutex serversGuard; // protects <servers>
        private List<Server> servers;
        private TcpListener tcp;
        private UdpClient udp;

        internal Fishtank()
        {
            serversGuard = new Mutex();
            servers = new List<Server>();
            tcp = new TcpListener(IPAddress.Any, 28860);
            udp = new UdpClient(28860);
            tcp.Start();
        }

        internal void Exec()
        {
            // see if there are any pending requests
            if (tcp.Pending())
                new Thread(Process).Start(tcp.AcceptTcpClient());

            // take update from registered server
            if (udp.Available > 0)
            {
                IPEndPoint id = new IPEndPoint(IPAddress.Any, 28860);
                byte[] data = udp.Receive(ref id);
                bool exists = Update(id.Address.ToString(), data[0]);
                // send something back
                if(exists)
                    udp.Send(new byte[] { 1 }, 1, id);
            }

            // see if any servers have fallen off the network
            Evaluate();
        }

        internal void Close()
        {
            udp.Close();
            tcp.Stop();
        }

        // see if the server is a valid server (does not already exist in the registry)
        private bool Valid(Server check)
        {
            serversGuard.WaitOne();
            try
            {
                foreach (Server server in servers)
                {
                    if (server.Name == check.Name || server.IpAddress == check.IpAddress)
                    {
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                serversGuard.ReleaseMutex();
            }
        }

        // add server to server list
        // called from multiple threads
        private string Add(Server server, out bool success)
        {
            // see if server alread exists
            if (!Valid(server))
            {
                success = false;
                return "A server with that name or IP Address already exists in the registry";
            }

            try
            {
                serversGuard.WaitOne();
                servers.Add(server);
            }
            finally
            {
                serversGuard.ReleaseMutex();
            }

            Console.WriteLine($"Registered Name: {server.Name}, Location: {server.Location}, IpAddress: {server.IpAddress}");
            success = true;
            return "";
        }

        // update a server's player count
        // return true if server exists in registry
        private bool Update(string ipaddr, int pc)
        {
            serversGuard.WaitOne();
            try
            {
                foreach (var server in servers)
                {
                    if (ipaddr == server.IpAddress)
                    {
                        server.Count = (byte)pc;
                        server.LastHeartbeat = UnixTime();
                        return true;
                    }
                }

                Console.WriteLine("couldn't find server " + ipaddr + " in the registry");
                return false;
            }
            finally
            {
                serversGuard.ReleaseMutex();
            }
        }

        // remove servers that have fallen off the network
        private void Evaluate()
        {
            serversGuard.WaitOne();
            try
            {
                foreach (var server in servers)
                {
                    if (UnixTime() - server.LastHeartbeat > 40)
                    {
                        if (!servers.Remove(server))
                            Console.WriteLine($"could not remove server \"{server.Name}\" because it doesn't exist in the registry");
                        else
                        {
                            Console.WriteLine($"lost connection to server \"{server.Name}\"");
                            break;
                        }
                    }
                }
            }
            finally
            {
                serversGuard.ReleaseMutex();
            }
        }

        // process client
        private void Process(object otcp)
        {
            try
            {
                var reader = new BinaryReader(((TcpClient)otcp).GetStream());

                // determine if this is a request for server list or registration for new server entry
                byte type = reader.ReadByte();
                switch(type)
                {
                    case 0:
                        Serve((TcpClient)otcp);
                        break;
                    case 1:
                        Register((TcpClient)otcp);
                        break;
                }
            }
            catch (IOException) { }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                ((TcpClient)otcp).Close();
            }
        }

        // serve the server list
        private void Serve(TcpClient tcp)
        {
            BinaryWriter tcpout = new BinaryWriter(tcp.GetStream());

            serversGuard.WaitOne();
            try
            {
                tcpout.Write((UInt64)servers.Count);

                foreach (Server server in servers)
                {
                    SendString(tcpout, server.IpAddress);
                    SendString(tcpout, server.Name);
                    SendString(tcpout, server.Location);
                    tcpout.Write(server.Count);
                }
            }
            finally
            {
                serversGuard.ReleaseMutex();
            }
        }

        // register a server
        private void Register(TcpClient tcp)
        {
            Server server = null;

            BinaryReader tcpin = new BinaryReader(tcp.GetStream());
            BinaryWriter tcpout = new BinaryWriter(tcp.GetStream());

            string ipaddr = ((IPEndPoint)tcp.Client.RemoteEndPoint).Address.ToString();
            string name = GetString(tcpin);
            string loc = GetString(tcpin);

            // try to connect back to server to make sure that it is accessible from the public internet
            try
            {
                TcpClient connectback = new TcpClient(ipaddr, 28856);
                if (!connectback.Connected)
                {
                    Console.WriteLine("Could not connect back");
                    return;
                }
                else
                {
                    var writer = new BinaryWriter(connectback.GetStream());
                    // tell fishtank-server that this is just a test connection
                    writer.Write((byte)1);
                }
                connectback.Close();

                server = new Server() { Location = loc, IpAddress = ipaddr, Name = name, Count = 0, LastHeartbeat = UnixTime() };

                bool added;
                string reason = Add(server, out added);
                if (!added)
                {
                    // notify client of failure
                    byte success = 0;
                    tcpout.Write(success);
                    SendString(tcpout, reason);
                }
                else
                {
                    // notify client of success
                    byte success = 1;
                    tcpout.Write(success);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(ipaddr + ": Couldn't connect back to server: " + e.Message);
                return;
            }
        }

        // sort by number of connected players
        private void Sort()
        {
            serversGuard.WaitOne();
            try
            {
                servers.Sort();
            }
            finally
            {
                serversGuard.ReleaseMutex();
            }
        }

        // pull a string off the network
        private string GetString(BinaryReader reader)
        {
            UInt32 count = reader.ReadUInt32();
            var data = new byte[count];

            reader.Read(data, 0, (Int32)count);
            return Encoding.ASCII.GetString(data);
        }

        // send a string on the network
        private void SendString(BinaryWriter writer, string s)
        {
            byte[] data = Encoding.ASCII.GetBytes(s);
            writer.Write((UInt32)s.Length);
            writer.Write(data);
        }

        private static Int64 UnixTime()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds();
        }
    }
}
