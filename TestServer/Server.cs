using MonstroeNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TestServer
{
    internal class Server : IEventNetListener
    {
        public int Port { get; set; }

        public Server(int port)
        {
            Port = port;
        }

        public void Start()
        {
            Console.WriteLine("Server Started...");

            NetSystem system = new NetSystem(Port);
            system.RegisterInterface(this);
            system.Listen();

            Console.WriteLine("Server initialized and is listening for connections...");

            while (true)
            {
                system.Update();
                Thread.Sleep(15);
            }
        }

        public void OnConnectionRequest(NetRequest request)
        {
            Console.WriteLine("Connection Request");
            request.Accept();
        }

        public void OnClientConnected(NetEndPoint remoteEndPoint)
        {
            Console.WriteLine("Client Connected");
            NetPacket packet = new NetPacket();
            packet.Write("Hello World!");
            remoteEndPoint.Send(packet, PacketProtocol.TCP);
            NetPacket packet2 = new NetPacket();
            packet2.Write("Goodbye"!);
            remoteEndPoint.Disconnect();
        }

        public void OnClientDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect)
        {
            Console.WriteLine("Disconnected from " + remoteEndPoint.EndPoint + ": " + disconnect.DisconnectCode.ToString());
        }

        public void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol)
        {
            throw new NotImplementedException();
        }

        public void OnNetworkError(SocketException socketException)
        {
            //Console.WriteLine("Error: " + socketException.SocketErrorCode.ToString());
            Console.WriteLine("Network Error: " + socketException.ToString());
        }
    }
}
