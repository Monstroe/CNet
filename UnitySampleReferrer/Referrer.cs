using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MonstroeNet;

namespace UnitySampleReferrer
{
    public class Client
    {
        public Guid ID { get; set; }
        public NetEndPoint NetEP { get; set; }
        public Room CurrentRoom { get; set; }
        public bool IsHost { get { return CurrentRoom != null && CurrentRoom.Host == this; } }

        public Client(NetEndPoint netEP)
        {
            NetEP = netEP;
        }
    }

    public class Room
    {
        public int ID { get; set; }
        public List<Client> Members { get; set; }
        public Client Host { get { return Members[0]; } }
        public List<Client> Guests { get { return Members.GetRange(1, Members.Count - 1); } }

        public Room(int id)
        {
            ID = id;
            Members = new List<Client>();
        }

        public List<Client> GetMembersExcept(Client clientToExclude)
        {
            List<Client> membersExcluding = new List<Client>();
            foreach (Client member in Members)
            {
                if (member != clientToExclude)
                {
                    membersExcluding.Add(member);
                }
            }
            return membersExcluding;
        }
    }

    public class Referrer : IEventNetListener
    {
        private static Referrer instance;
        public static Referrer Instance
        {
            get
            {
                if (instance == null)
                    instance = new Referrer();
                return instance;
            }
        }

        public int Port
        {
            get { return listener.Port; }
            set { listener.Port = value; }
        }

        public delegate void PacketHandler(Client client, NetPacket packet);

        private NetListener listener;

        private Dictionary<NetEndPoint, Client> clients;
        private Dictionary<int, Room> rooms;
        private Dictionary<string, PacketHandler> packetHandlers;

        private bool running;

        private Referrer()
        {
            listener = new NetListener();
            listener.RegisterInterface(this);

            clients = new Dictionary<Guid, Client>();
            rooms = new Dictionary<int, Room>();
            packetHandlers = new Dictionary<string, PacketHandler>()
            {
                { "CREATEROOM", CreateRoom },
                { "JOINROOM", JoinRoom },
                { "LEAVEROOM", LeaveRoom },
                { "STARTROOM", StartRoom },
                { "CLOSEROOM", CloseRoom }
            };

            running = false;
        }

        public void Start(int port)
        {
            Console.WriteLine("Starting Referrer...");
            Port = port;
            listener.Listen();

            running = true;

            while (running)
            {
                listener.Update();
                Thread.Sleep(15);
            }

            Close();
        }

        public void Stop()
        {
            running = false;
        }

        public void Close()
        {
            Console.WriteLine("Closing Referrer...");
            listener.Close(true);
        }

        public void OnClientConnected(NetEndPoint remoteEndPoint)
        {
            var id = Guid.NewGuid();
            var packet = new NetPacket();
            clients.Add(remoteEndPoint, new Client(remoteEndPoint));
            packet.Write(id.ToString());
            Send(clients[remoteEndPoint], packet, PacketProtocol.TCP);
            Console.WriteLine("Client " + remoteEndPoint.EndPoint.ToString() + " Connected!");
            Console.WriteLine("Number of Clients Online: " + clients.Count);
        }

        public void OnClientDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect)
        {
            Console.WriteLine("Client " + remoteEndPoint.EndPoint.ToString() + " disconnected: " + disconnect.DisconnectCode.ToString());

            if (clients[peer.Id].CurrentRoom != null)
            {
                if (clients[peer.Id].IsHost)
                {
                    CloseRoom(clients[peer.Id].CurrentRoom);
                }
                else
                {
                    LeaveRoom(clients[peer.Id], clients[peer.Id].CurrentRoom);
                }
            }

            clients.Remove(peer.Id);
            Console.WriteLine("Number of Clients Online: " + clients.Count);
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

        public void Send(Client client, NetPacket packet, PacketProtocol packetProtocol)
        {
            client.NetEP.Send(packet, packetProtocol);
        }

        public void Send(List<Client> clients, NetPacket packet, PacketProtocol packetProtocol)
        {
            foreach (Client client in clients)
            {
                Send(client, packet, packetProtocol);
            }
        }

        static void Main(string[] args)
        {
            Referrer.Instance.Start(7777);
        }
    }
}
