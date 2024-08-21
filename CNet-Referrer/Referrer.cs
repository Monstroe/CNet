using System.Net.Sockets;
using CNet;

namespace CNet_Referrer;

public class Referrer : IEventNetListener
{
    public static Referrer Instance { get; } = new Referrer();

    public const int POLL_RATE = 15;
    public const int MAX_ROOM_AMOUNT = 10000;

    public delegate void PacketHandler(Client client, NetPacket packet);

    public string Address
    {
        get { return Listener.Address; }
        set { Listener.Address = value; }
    }

    public int Port
    {
        get { return Listener.Port; }
        set { Listener.Port = value; }
    }

    public NetListener Listener { get; }
    public Dictionary<NetEndPoint, Client> Clients { get; }
    public Dictionary<int, Room> Rooms { get; }

    private Dictionary<ServiceReceiveType, PacketHandler> packetHandlers;
    private bool running;

    private Referrer()
    {
        Listener = new NetListener();
        Listener.RegisterInterface(this);


        Clients = new Dictionary<NetEndPoint, Client>();
        Rooms = new Dictionary<int, Room>();
        packetHandlers = new Dictionary<ServiceReceiveType, PacketHandler>()
        {
            { ServiceReceiveType.CreateRoom, PacketReceiver.Instance.CreateRoom },
            { ServiceReceiveType.JoinRoom, PacketReceiver.Instance.JoinRoom },
            { ServiceReceiveType.LeaveRoom, PacketReceiver.Instance.LeaveRoom },
            { ServiceReceiveType.StartRoom, PacketReceiver.Instance.StartRoom },
            { ServiceReceiveType.CloseRoom, PacketReceiver.Instance.CloseRoom }
        };

        running = false;
    }

    public void Start(string address, int port)
    {
        Console.WriteLine("Starting Referrer...");
        Address = address;
        Port = port;
        Listener.Listen();
        running = true;
        Console.WriteLine("Referrer Started, waiting for connections...");

        while (running)
        {
            Listener.Update();
            Thread.Sleep(POLL_RATE);
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
        Listener.Close(true);
    }

    public void OnConnectionRequest(NetRequest request)
    {
        Console.WriteLine("Connection Request from: " + request.ClientEndPoint.ToString());
        request.Accept();
    }

    public void OnClientConnected(NetEndPoint remoteEndPoint)
    {
        Console.WriteLine("Client " + remoteEndPoint.TCPEndPoint.ToString() + " Connected");
        var client = new Client(Guid.NewGuid(), remoteEndPoint);
        Clients.Add(remoteEndPoint, client);
        PacketSender.Instance.ID(client, client.ID);
        Console.WriteLine("Number of Clients Online: " + Clients.Count);
    }

    public void OnClientDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect)
    {
        Console.WriteLine("Client " + remoteEndPoint.TCPEndPoint.ToString() + " Disconnected: " + disconnect.DisconnectCode.ToString());

        if (Clients[remoteEndPoint].CurrentRoom != null)
        {
            var client = Clients[remoteEndPoint];
            if (client.IsHost)
            {
                CloseRoom(client.CurrentRoom);
            }
            else
            {
                LeaveRoom(client, client.CurrentRoom);
            }
        }

        Clients.Remove(remoteEndPoint);
        Console.WriteLine("Number of Clients Online: " + Clients.Count);
    }

    public void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol)
    {
        if (packet.Length < 2)
        {
            Console.Error.WriteLine("Invalid Packet Received from " + remoteEndPoint.TCPEndPoint.ToString());
            return;
        }

        ServiceReceiveType command = (ServiceReceiveType)packet.ReadShort();
        if (packetHandlers.TryGetValue(command, out PacketHandler? handler))
        {
            handler(Clients[remoteEndPoint], packet);
        }
        else
        {
            if (Clients[remoteEndPoint].CurrentRoom != null)
            {
                if (Clients[remoteEndPoint].IsHost)
                {
                    Send(Clients[remoteEndPoint].CurrentRoom.Guests, packet, protocol);
                }
                else
                {
                    Send(Clients[remoteEndPoint].CurrentRoom.Host, packet, protocol);
                }
            }
            else
            {
                Console.Error.WriteLine("Client " + remoteEndPoint.TCPEndPoint.ToString() + " sent invalid packet");
            }
        }
    }

    public void OnNetworkError(NetEndPoint remoteEndPoint, SocketException socketException)
    {
        Console.Error.WriteLine("Network Error: " + socketException.SocketErrorCode.ToString());
    }

    public void Send(Client client, NetPacket packet, PacketProtocol protocol)
    {
        client.RemoteEP.Send(packet, protocol);
    }

    public void Send(List<Client> clients, NetPacket packet, PacketProtocol protocol)
    {
        foreach (Client client in clients)
        {
            Send(client, packet, protocol);
        }
    }

    public void LeaveRoom(Client client, Room room)
    {
        Console.WriteLine("Client " + client.RemoteEP.TCPEndPoint.ToString() + " left room with code: " + room.ID);
        PacketSender.Instance.MemberLeft(client, client.ID);
        room.Members.Remove(client);
    }

    public void CloseRoom(Room room)
    {
        Console.WriteLine("Closing room with code: " + room.ID);
        foreach (Client member in room.Members)
        {
            member.CurrentRoom = null;
        }
        PacketSender.Instance.RoomClosed(room.Members);
        Rooms.Remove(room.ID);
    }

    public int GenerateRoomID()
    {
        var random = new Random();
        int randomNumber = random.Next(0, MAX_ROOM_AMOUNT);
        if (Rooms.ContainsKey(randomNumber))
        {
            return GenerateRoomID();
        }
        return randomNumber;
    }

    static void Main(string[] args)
    {
        Referrer.Instance.Start("localhost", 7777);
    }
}
