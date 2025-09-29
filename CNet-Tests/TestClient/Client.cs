using System.Net.Sockets;
using System.Reflection;
using CNet;

namespace TestClient;

[NetSyncable(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)]
public class NetClass
{
    public int IntValue { get; set; }
    public string? StringValue { get; set; }
    private float[]? FloatValue;
    public NetStruct NetStructValue { get; set; }

    public void SetFloatValue(float[] value)
    {
        FloatValue = value;
    }

    public float[]? GetFloatValue()
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
    public string? Address
    {
        get { return client.Address; }
    }

    public int Port
    {
        get { return client.Port; }
    }

    private NetClient client;

    private int packetCount = 0;

    private Client()
    {
        client = new NetClient();
        client.RegisterInterface(this);
        client.Serializer.RegisterAssembly(Assembly.GetEntryAssembly()!);
    }

    public void Start(string address, int port, string connectionKey)
    {
        Console.WriteLine("Client Starting...");

        client.Connect(address, port, connectionKey);
        Console.WriteLine("Client initialized...");
        while (true)
        {
            client.Update();
            Thread.Sleep(15);
        }
    }

    public void OnConnected(NetEndPoint remoteEP)
    {
        Console.WriteLine("Connected to " + remoteEP.TCPEndPoint);
    }

    public void OnDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect)
    {
        Console.WriteLine("Disconnected from " + remoteEP.TCPEndPoint + ": " + disconnect.DisconnectCode.ToString() + (disconnect.DisconnectData != null ? " - " + disconnect.DisconnectData.ReadString() : ""));
    }

    public void OnPacketReceived(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol)
    {
        NetClass netClass = packet.DeserializeClass<NetClass>();
        Console.WriteLine("Packet Received from " + remoteEP.TCPEndPoint + " with class: " + netClass.IntValue + ", " + netClass.StringValue + ", (" + netClass.GetFloatValue()![0] + ", " + netClass.GetFloatValue()![1] + ", " + netClass.GetFloatValue()![2] + ")" + ", struct(" + netClass.NetStructValue.IntValue + ", " + netClass.NetStructValue.StringValue + ", " + netClass.NetStructValue.FloatValue + ")");
        packetCount++;

        if (packetCount >= 100)
        {
            client.Disconnect();
        }
        else
        {
            using (NetPacket respPacket = new NetPacket(client.System, TransportProtocol.UDP))
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
                remoteEP.Send(respPacket, TransportProtocol.UDP);
            }
        }
    }

    public void OnNetworkError(NetEndPoint? remoteEP, SocketError error)
    {
        Console.WriteLine("Error: " + error.ToString());
    }

    // Main Method
    static void Main(string[] args)
    {
        Client.Instance.Start("127.0.0.1", 7777, "CNetTest");
    }
}
