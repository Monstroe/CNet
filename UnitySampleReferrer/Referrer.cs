using System;
using System.Net.Sockets;
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

            clients = new Dictionary<NetEndPoint, Client>();
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

            if (clients[remoteEndPoint].CurrentRoom != null)
            {
                if (clients[remoteEndPoint].IsHost)
                {
                    CloseRoom(clients[remoteEndPoint].CurrentRoom);
                }
                else
                {
                    LeaveRoom(clients[remoteEndPoint], clients[remoteEndPoint].CurrentRoom);
                }
            }

            clients.Remove(remoteEndPoint);
            Console.WriteLine("Number of Clients Online: " + clients.Count);
        }

        public void OnConnectionRequest(NetRequest request)
        {
            Console.WriteLine("Connection Request: " + request.ClientEndPoint.ToString());
            request.Accept();
        }

        public void OnNetworkError(SocketException socketException)
        {
            Console.WriteLine("Exception: " + socketException.SocketErrorCode.ToString());
        }

        public void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol)
        {
            string command = packet.ReadString();

            if (protocol == PacketProtocol.TCP)
            {
                Console.WriteLine("Packet Received from " + remoteEndPoint.EndPoint.ToString() + ": " + command);
            }

            if (packetHandlers.ContainsKey(command))
            {
                packetHandlers[command.ToUpper()](clients[remoteEndPoint], packet);
            }
            else
            {
                if (clients[remoteEndPoint].CurrentRoom != null)
                {
                    if (clients[remoteEndPoint].IsHost)
                    {
                        Send(clients[remoteEndPoint].CurrentRoom.Guests, packet, protocol);
                    }
                    else
                    {
                        Send(clients[remoteEndPoint].CurrentRoom.Host, packet, protocol);
                    }
                }
                else
                {
                    Console.Error.WriteLine("Client " + remoteEndPoint.EndPoint.ToString() + " Sent Invalid Packet: " + packet);
                }
            }
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

        public void LeaveRoom(Client client, Room room)
        {
            Console.WriteLine("Client " + client.NetEP.EndPoint.ToString() + " Left Room with Code: " + room.ID);
            Send(room.Members, MemberLeftPacket(client.ID), PacketProtocol.TCP);
            room.Members.Remove(client);
        }

        public void CloseRoom(Room room)
        {
            Console.WriteLine("Closing Room with Code: " + room.ID);
            foreach (Client member in room.Members)
            {
                member.CurrentRoom = null;
            }
            Send(room.Members, RoomClosedPacket(), PacketProtocol.TCP);
            rooms.Remove(room.ID);
        }

        //----------------------------------PACKET HANDLERS----------------------------------//

        private void CreateRoom(Client client, NetPacket packet)
        {
            Room room = new Room(GenerateRoomID());
            room.Members.Add(client);
            client.CurrentRoom = room;
            rooms.Add(room.ID, room);

            Console.WriteLine("Creating Room... New Room Code for Client " + client.NetEP.EndPoint.ToString() + ": " + room.ID);
            Send(client, RoomCodePacket(room.ID), PacketProtocol.TCP);
        }

        private void JoinRoom(Client client, NetPacket packet)
        {
            if (client.CurrentRoom != null)
            {
                Console.Error.WriteLine("Client " + client.NetEP.EndPoint.ToString() + " Attempted to Join a Room Despite Already Being in One");
                Send(client, InvalidPacket("Client Already in Room"), PacketProtocol.TCP);
                return;
            }

            try
            {
                int roomID = packet.ReadInt();
                if (rooms.ContainsKey(roomID))
                {
                    Room room = rooms[roomID];
                    room.Members.Add(client);
                    client.CurrentRoom = room;

                    Console.WriteLine("Client " + client.NetEP.EndPoint.ToString() + " Joining Room with Code: " + room.ID);
                    foreach (Client member in room.Members)
                    {
                        Send(member, MemberJoinedPacket(client.ID), PacketProtocol.TCP);
                    }

                    Send(client, RoomMembersPacket(room.Members), PacketProtocol.TCP);
                }
                else
                {
                    Console.Error.WriteLine("Client " + client.NetEP.EndPoint.ToString() + " Sent Room Code that does not Exist: " + roomID);
                    Send(client, InvalidPacket("Nonexistent Room Code"), PacketProtocol.TCP);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Client " + client.NetEP.EndPoint.ToString() + " Sent Invalid Room Code: " + e.Message);
                Send(client, InvalidPacket("Invalid Room Code"), PacketProtocol.TCP);
            }
        }

        private void LeaveRoom(Client client, NetPacket packet)
        {
            if (client.CurrentRoom == null)
            {
                Console.Error.WriteLine("Client " + client.NetEP.EndPoint.ToString() + " Attempted to Leave a Room Despite not Being in One");
                Send(client, InvalidPacket("Client Not in Room"), PacketProtocol.TCP);
                return;
            }

            if (client.IsHost)
            {
                Console.Error.WriteLine("Client " + client.NetEP.EndPoint.ToString() + " Attempted to Leave a Room as a Host");
                Send(client, InvalidPacket("Host Attempted to Leave Room"), PacketProtocol.TCP);
                return;
            }

            LeaveRoom(client, client.CurrentRoom);
        }

        private void StartRoom(Client client, NetPacket packet)
        {
            if (client.CurrentRoom == null)
            {
                Console.Error.WriteLine("Client " + client.NetEP.EndPoint.ToString() + " Attempted to Start a Room Despite not Being in One");
                Send(client, InvalidPacket("Client Not in Room"), PacketProtocol.TCP);
                return;
            }

            if (!client.IsHost)
            {
                Console.Error.WriteLine("Client " + client.NetEP.EndPoint.ToString() + " Attempted to Start a Room as a Guest");
                Send(client, InvalidPacket("Guest Attempted to Start Room"), PacketProtocol.TCP);
                return;
            }

            Console.WriteLine("Starting Room... With Code: " + client.CurrentRoom.ID);
            Send(client.CurrentRoom.Members, RoomStartPacket(), PacketProtocol.TCP);
        }

        private void CloseRoom(Client client, NetPacket packet)
        {
            if (client.CurrentRoom == null)
            {
                Console.Error.WriteLine("Client " + client.NetEP.EndPoint.ToString() + " Attempted to Leave a Room Despite not Being in One");
                Send(client, InvalidPacket("Client Not in Room"), PacketProtocol.TCP);
                return;
            }

            if (!client.IsHost)
            {
                Console.Error.WriteLine("Client " + client.NetEP.EndPoint.ToString() + " Attempted to Close a Room as a Guest");
                Send(client, InvalidPacket("Guest Attempted to Close Room"), PacketProtocol.TCP);
            }

            CloseRoom(client.CurrentRoom);
        }

        //----------------------------------PACKET BUILDERS----------------------------------//

        public static NetPacket IDPacket(int id)
        {
            var packet = new NetPacket();
            packet.Write("ID");
            packet.Write(id);
            return packet;
        }

        public NetPacket RoomCodePacket(int roomCode)
        {
            var packet = new NetPacket();
            packet.Write("ROOMCODE");
            packet.Write(roomCode);
            return packet;
        }

        public NetPacket RoomMembersPacket(List<Client> members)
        {
            var packet = new NetPacket();
            packet.Write("ROOMMEMBERS");
            foreach (Client member in members)
            {
                packet.Write(member.ID.ToString());
            }

            return packet;
        }

        public NetPacket MemberJoinedPacket(Guid memberID)
        {
            var packet = new NetPacket();
            packet.Write("MEMBERJOIN");
            packet.Write(memberID.ToString());
            return packet;
        }

        public NetPacket MemberLeftPacket(Guid memberID)
        {
            var packet = new NetPacket();
            packet.Write("MEMBERLEFT");
            packet.Write(memberID.ToString());
            return packet;
        }

        public NetPacket RoomStartPacket()
        {
            var packet = new NetPacket();
            packet.Write("ROOMSTART");
            return packet;
        }

        public NetPacket RoomClosedPacket()
        {
            var packet = new NetPacket();
            packet.Write("ROOMCLOSED");
            return packet;
        }

        public NetPacket InvalidPacket(string errorMessage)
        {
            var packet = new NetPacket();
            packet.Write("INVALID");
            packet.Write(errorMessage);
            return packet;
        }

        //-----------------------------------------------------------------------------------//

        public int GenerateRoomID()
        {
            var random = new Random();
            int randomNumber = random.Next(0, 10000);
            if (rooms.ContainsKey(randomNumber))
            {
                return GenerateRoomID();
            }
            return randomNumber;
        }

        static void Main(string[] args)
        {
            Referrer.Instance.Start(7777);
        }
    }
}
