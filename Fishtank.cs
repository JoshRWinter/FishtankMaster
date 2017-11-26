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

        internal Fishtank()
        {
            serversGuard = new Mutex();
            servers = new List<Server>();
            tcp = new TcpListener(IPAddress.Any, 28860);
            tcp.Start();
        }

        internal void Exec()
        {
            while(true)
            {
                Handle(tcp.AcceptTcpClient());
            }
        }

        // add server to server list
        // called from multiple threads
        internal string Add(Server server, out bool success)
        {
            try
            {
                serversGuard.WaitOne();
                // see if server alread exists
                foreach(Server srv in servers)
                {
                    if(server.Name == srv.Name)
                    {
                        success = false;
                        return "A server with this name already exists in the registry.";
                    }
                    else if(server.IpAddress == srv.IpAddress)
                    {
                        success = false;
                        return "A server with this IP address already exists in the registry.";
                    }
                }
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

        // process client
        private void Handle(TcpClient tcp)
        {
            var thread = new Thread(Start);
            thread.Start(tcp);
        }

        private void Start(object otcp)
        {
            var reader = new BinaryReader(((TcpClient)otcp).GetStream());

            // determine if this is a request for server list or registration for new server entry
            byte type = reader.ReadByte();
            if(type == 0)
            {
                Serve((TcpClient)otcp);
            }
            else
            {
                Register((TcpClient)otcp);
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
                foreach(Server server in servers)
                {
                    SendString(tcpout, server.IpAddress);
                    SendString(tcpout, server.Name);
                    SendString(tcpout, server.Location);
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
            BinaryReader tcpin = new BinaryReader(tcp.GetStream());
            BinaryWriter tcpout = new BinaryWriter(tcp.GetStream());

            string ipaddr = ((IPEndPoint)tcp.Client.RemoteEndPoint).Address.ToString();
            string name = GetString(tcpin);
            string loc = GetString(tcpin);

            tcp.Close();

            try
            {
                TcpClient connectback = new TcpClient(ipaddr, 28857);
                if(!connectback.Connected)
                {
                    Console.WriteLine("Could not connect back");
                    return;
                }
                var writer = new BinaryWriter(connectback.GetStream());
                var reader = new BinaryReader(connectback.GetStream());

                Server server = new Server() { Location = loc, IpAddress = ipaddr, Name = name };

                bool added;
                string reason = Add(server, out added);
                if(!added)
                {
                    // notify client of failure
                    byte success = 0;
                    writer.Write(success);
                    SendString(writer, reason);
                }
                else
                {
                    // notify client of success
                    byte success = 1;
                    writer.Write(success);
                }
            }
            catch(SocketException e)
            {
                Console.WriteLine(ipaddr + ": Couldn't connect back to server: " + e.Message);
            }
        }

        // pull a strincg off the network
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
    }
}
