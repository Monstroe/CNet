using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MonstroeNet;

namespace UnitySampleReferrer
{
    public class Referrer : IEventNetListener
    {
        private static Referrer main;
        public static Referrer Main
        {
            get
            {
                if(main == null)
                    main = new Referrer();
                return main;
            }
        }

        public int Port
        {
            get { return listener.Port; }
            set { listener.Port = value; }
        }

        private NetListener listener;

        private Referrer()
        {
            listener = new NetListener();
            listener.RegisterInterface(this);
        }

        public void Start(int port)
        {
            Console.WriteLine("Server Starting...");
            Port = port;

            listener.Listen();
            Console.WriteLine("Server initialized, waiting for clients...");
            while (true)
            {
                listener.Update();
                Thread.Sleep(15);
            }
        }

        public void OnClientConnected(NetEndPoint remoteEndPoint)
        {
            Console.WriteLine("Client " + remoteEndPoint.EndPoint + " connected!");
            using (NetPacket packet  = new NetPacket())
            {
                packet.Write("Hello Client!");
                Console.WriteLine("Packet 1: " + packet.ReadString(false));
                remoteEndPoint.Send(packet, PacketProtocol.TCP);
            }

            using (NetPacket packet2 = new NetPacket())
            {
                packet2.Write("Hello Client 2!");
                Console.WriteLine("Packet 2: " + packet2.ReadString(false));
                remoteEndPoint.Send(packet2, PacketProtocol.TCP);
            }
            //remoteEndPoint.Disconnect();
            remoteEndPoint.DisconnectForcefully();
        }

        public void OnClientDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect)
        {
            Console.WriteLine("Client " + remoteEndPoint.EndPoint + " disconnected: " + disconnect.DisconnectCode.ToString());
        }

        public void OnConnectionRequest(NetRequest request)
        {
            Console.WriteLine("Connection Request: " + request.ClientEndPoint.ToString());
            request.Accept();
            //request.Deny();
        }

        public void OnNetworkError(SocketException socketException)
        {
            //Console.WriteLine("Exception: " + socketException.SocketErrorCode.ToString());
            Console.WriteLine("Exception: " + socketException.ToString());
        }

        public void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol)
        {
            Console.WriteLine("Packet Received!");
        }
    }
}
