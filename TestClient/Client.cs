using MonstroeNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TestClient
{
    internal class Client : IEventNetClient
    {
        private int testCounter = 100;

        private static Client instance;
        public static Client Instance
        {
            get
            {
                if (instance == null)
                    instance = new Client();
                return instance;
            }
        }

        public string IPAddress 
        {
            get { return client.Address; }
            set { client.Address = value; }
        }

        public int Port
        {
            get { return client.Port; }
            set { client.Port = value; }
        }

        private NetClient client;

        private Client()
        {
            client = new NetClient();
            client.RegisterInterface(this);
        }

        public void Start(string address, int port)
        {
            Console.WriteLine("Client Starting...");
            client.Address = address;
            client.Port = port;

            client.Connect();
            Console.WriteLine("Client Initialized");
            int counter = 0;
            while (true)
            {
                client.Update();
                Thread.Sleep(15);

                // UDP Packet Test
                if(client.IsConnected && client.RemoteUDPEndPoint != null) 
                {
                    NetPacket packet = new NetPacket();
                    packet.Write("UDP: ");
                    packet.Write(counter);
                    counter++;
                    //client.RemoteUDPEndPoint.Send(packet, PacketProtocol.UDP);
                }
            }
        }

        public void OnConnected(NetEndPoint remoteEndPoint)
        {
            Console.WriteLine("Connected to " + remoteEndPoint.EndPoint);
            NetPacket packet = new NetPacket();
            packet.Write("Hello Server!");
            remoteEndPoint.Send(packet, PacketProtocol.TCP);
            //client.Disconnect();
            //client.Close(true);
            //remoteEndPoint.Send(packet, PacketProtocol.TCP);

            //NetPacket helloServer = new NetPacket();
            //helloServer.Write("Hello Server!");
            //remoteEndPoint.Send(helloServer, PacketProtocol.TCP);
        }

        public void OnDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect)
        {
            Console.WriteLine("Disconnected from " + remoteEndPoint.EndPoint + ": " + disconnect.DisconnectCode.ToString() + (disconnect.DisconnectData != null ? ". Message: " + disconnect.DisconnectData.ReadString() : ""));

            /*if(testCounter == 0) 
            {
                if(disconnect.DisconnectCode == DisconnectCode.ConnectionRejected)
                    PassedTest(0);
                else
                    FailedTest(0);
                client.Connect();
                return;
            }

            if(testCounter == 1) 
            {
                if(disconnect.DisconnectCode == DisconnectCode.ConnectionClosed)
                    PassedTest(1);
                else
                    FailedTest(1);
                client.Connect();
                return;
            }

            if(testCounter == 2) 
            {
                if(disconnect.DisconnectCode == DisconnectCode.ConnectionClosedForcefully)
                    PassedTest(2);
                else
                    FailedTest(2);
                client.Connect();
                return;
            }

            if(testCounter == 4) 
            {
                if(disconnect.DisconnectCode == DisconnectCode.ConnectionClosedWithMessage)
                    PassedTest(4);
                else
                    FailedTest(4);
                client.Connect();
                return;
            }

            if(testCounter == 5) 
            {
                if(disconnect.DisconnectCode == DisconnectCode.ConnectionClosed)
                    PassedTest(5);
                else
                    FailedTest(5);
                return;
            }*/
        }

        public void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol)
        {
            string message = packet.ReadString();
            Console.WriteLine("Packet Received from " + remoteEndPoint.EndPoint.ToString() + ": " + message);

            /*NetPacket response = new NetPacket();
            response.Write("Hello Server!");
            remoteEndPoint.Send(response, PacketProtocol.TCP);

            if(testCounter == 3) 
            {
                if(message == "Hello Client!")
                    PassedTest(3);
                else
                    FailedTest(3);
                remoteEndPoint.Send(response, PacketProtocol.TCP);
                return;
            }*/
        }

        public void OnNetworkError(SocketException socketException)
        {
            Console.WriteLine("Network Error: " + socketException.SocketErrorCode.ToString());
            //Console.WriteLine("Network Error: " + socketException.ToString());
        }

        // Main Method
        static void Main(string[] args)
        {
            Client.Instance.Start("127.0.0.1", 7777);
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
