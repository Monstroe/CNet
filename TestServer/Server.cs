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
        private int testCounter = 100;

        private static Server instance;
        public static Server Instance
        {
            get
            {
                if (instance == null)
                    instance = new Server();
                return instance;
            }
        }

        public int Port 
        {
            get { return listener.Port; }
            set { listener.Port = value; }
        }

        private NetListener listener;

        private Server()
        {
            listener = new NetListener();
            listener.RegisterInterface(this);
        }

        public void Start(int port)
        {
            Console.WriteLine("Server Starting...");
            listener.Port = port;

            listener.Listen();
            Console.WriteLine("Server initialized");
            Console.WriteLine("Waiting for connections...");
            int counter = 0;
            while (true)
            {
                listener.Update();
                Thread.Sleep(15);

                if(listener.RemoteUDPEndPoints.Count > 0) 
                {
                    NetPacket packet = new NetPacket();
                    packet.Write("UDP: ");
                    packet.Write(counter);
                    counter++;
                    //foreach(NetEndPoint remoteEP in listener.RemoteUDPEndPoints)
                        //remoteEP.Send(packet, PacketProtocol.UDP);
                }
            }
        }

        public void OnConnectionRequest(NetRequest request)
        {
            Console.WriteLine("Connection Request from " + request.ClientEndPoint.ToString());

            if(testCounter == 0)
                request.Deny();
            else
                request.Accept();
        }

        public void OnClientConnected(NetEndPoint remoteEndPoint)
        {
            Console.WriteLine("Client " + remoteEndPoint.EndPoint.ToString() + " Connected");
            NetPacket hello2 = new NetPacket();
            hello2.Write("Test Packet");
            remoteEndPoint.Send(hello2, PacketProtocol.TCP);

            if(testCounter == 1) 
            {
                NetPacket hello = new NetPacket();
                hello.Write("Test Packet");
                remoteEndPoint.Send(hello, PacketProtocol.TCP);
                //remoteEndPoint.Disconnect();
                return;
            }

            NetPacket greeting = new NetPacket();
            greeting.Write("Hello Client!");

            if(testCounter == 2) 
            {
                remoteEndPoint.Send(greeting, PacketProtocol.TCP);
                remoteEndPoint.DisconnectForcefully();
                return;
            }

            if(testCounter == 3) 
            {
                remoteEndPoint.Send(greeting, PacketProtocol.TCP);
                return;
            }

            if(testCounter == 5) 
            {
                listener.Close(true);
                return;
            }
        }

        public void OnClientDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect)
        {
            Console.WriteLine("Disconnected from " + remoteEndPoint.EndPoint.ToString() + ": " + disconnect.DisconnectCode.ToString() + (disconnect.DisconnectData != null ? ". Message: " + disconnect.DisconnectData.ReadString() : ""));

            
            if(testCounter == 0) 
            {
                if(disconnect.DisconnectCode == DisconnectCode.ConnectionRejected)
                    PassedTest(0);
                else
                    FailedTest(0);
                return;
            }

            if(testCounter == 1) 
            {
                if(disconnect.DisconnectCode == DisconnectCode.ConnectionClosed)
                    PassedTest(1);
                else
                    FailedTest(1);
                return;
            }
            
            if(testCounter == 2)
            {
                if(disconnect.DisconnectCode == DisconnectCode.ConnectionClosedForcefully)
                    PassedTest(2);
                else
                    FailedTest(2);
                return;
            }
            
            if(testCounter == 4) 
            {
                if(disconnect.DisconnectCode == DisconnectCode.ConnectionClosedWithMessage)
                    PassedTest(4);
                else
                    FailedTest(4);
                return;
            }
            
            if(testCounter == 5) 
            {
                if(disconnect.DisconnectCode == DisconnectCode.ConnectionClosed)
                    PassedTest(5);
                else
                    FailedTest(5);
                return;
            }
        }

        public void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol)
        {
            string message = packet.ReadString();
            Console.WriteLine("Packet Received from " + remoteEndPoint.EndPoint.ToString() + ": " + message);

            NetPacket goodbye = new NetPacket();
            goodbye.Write("Goodbye!");

            if(testCounter == 3) 
            {
                if(message == "Hello Server!")
                    PassedTest(3);
                else
                    FailedTest(3);
                remoteEndPoint.Disconnect(goodbye);
                return;
            }
        }

        public void OnNetworkError(SocketException socketException)
        {
            Console.WriteLine("Network Error: " + socketException.SocketErrorCode.ToString());
            //Console.WriteLine("Network Error: " + socketException.ToString());
        }

        static void Main(string[] args)
        {
            Server.Instance.Start(7777);
        }

        private void PassedTest(int testNumber) 
        {
            Console.WriteLine("TEST " + testNumber + " PASS ------------------------");
            testCounter = testNumber + 1;
        }

        private void FailedTest(int testNumber) 
        {
            Console.WriteLine("TEST " + testNumber + " FAIL ------------------------");
            testCounter = testNumber + 1;
        }
    }
}
