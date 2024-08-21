using System.Net.Sockets;
using System.Reflection;
using CNet;

namespace TestClient;

[NetSyncable(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)]
public class NetClass
{
    public int IntValue { get; set; }
    public string StringValue { get; set; }
    private float[] FloatValue;
    public NetStruct NetStructValue { get; set; }

    public void SetFloatValue(float[] value)
    {
        FloatValue = value;
    }

    public float[] GetFloatValue()
    {
        return FloatValue;
    }
}

[NetSyncable]
public struct NetStruct
{
    public int IntValue;
    public string StringValue;
    public float FloatValue { get; set; }
}

class Client : IEventNetClient
{
    public static Client Instance { get; } = new Client();
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

    private int packetCount = 0;

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
        Console.WriteLine("Client initialized...");
        while (true)
        {
            client.Update();
            Thread.Sleep(15);
        }
    }

    public void OnConnected(NetEndPoint remoteEndPoint)
    {
        Console.WriteLine("Connected to " + remoteEndPoint.TCPEndPoint);
    }

    public void OnDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect)
    {
        Console.WriteLine("Disconnected from " + remoteEndPoint.TCPEndPoint + ": " + disconnect.DisconnectCode.ToString() + (disconnect.DisconnectData != null ? " - " + disconnect.DisconnectData.ReadString() : ""));
    }

    public void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol)
    {
        NetClass netClass = packet.DeserializeClass<NetClass>();
        Console.WriteLine("Packet Received from " + remoteEndPoint.TCPEndPoint + " with class: " + netClass.IntValue + ", " + netClass.StringValue + ", (" + netClass.GetFloatValue()[0] + ", " + netClass.GetFloatValue()[1] + ", " + netClass.GetFloatValue()[2] + ")" + ", struct(" + netClass.NetStructValue.IntValue + ", " + netClass.NetStructValue.StringValue + ", " + netClass.NetStructValue.FloatValue + ")");
        packetCount++;

        if (packetCount >= 100)
        {
            client.Disconnect();
        }

        using (NetPacket respPacket = new NetPacket(client.System, PacketProtocol.UDP))
        {
            NetStruct newNetStruct = new NetStruct();
            newNetStruct.IntValue = 10;
            newNetStruct.StringValue = "Hello World!";
            newNetStruct.FloatValue = 3.14f;

            NetClass newNetClass = new NetClass();
            newNetClass.IntValue = 20;
            newNetClass.StringValue = "Goodbye World!";
            newNetClass.SetFloatValue(new float[] { 1.0f, 2.0f, 3.0f });
            newNetClass.NetStructValue = newNetStruct;

            respPacket.SerializeClass(newNetClass);
            remoteEndPoint.Send(respPacket, PacketProtocol.UDP);
        }
    }

    public void OnNetworkError(NetEndPoint remoteEndPoint, SocketException socketException)
    {
        Console.WriteLine("Error: " + socketException.SocketErrorCode.ToString());
    }

    // Main Method
    static void Main(string[] args)
    {
        Client.Instance.Start("127.0.0.1", 7777);
    }
}
