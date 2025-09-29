using System.Net.Sockets;
using System.Reflection;
using CNet;

namespace TestServer;

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

class Server : IEventNetListener
{
    public static Server Instance { get; } = new Server();

    public int Port
    {
        get { return listener.Port; }
    }

    private readonly NetListener listener;

    private int packetCount = 0;
    private string connectionKey;

    private Server()
    {
        listener = new NetListener();
        listener.RegisterInterface(this);
        listener.Serializer.RegisterAssembly(Assembly.GetEntryAssembly()!);
        connectionKey = string.Empty;
    }

    public void Start(int port, string connectionKey)
    {
        Console.WriteLine("Server Started...");
        this.connectionKey = connectionKey;

        listener.Listen(port);
        Console.WriteLine("Server initialized and is listening for connections...");
        while (true)
        {
            listener.Update();
            Thread.Sleep(15);
        }
    }

    public void OnConnectionRequest(NetRequest request)
    {
        Console.WriteLine("Connection Request: " + request.ConnectingEP.TCPEndPoint.ToString() + ", ID: " + request.ConnectingEP.ID);
        request.AcceptIfKey(connectionKey);
    }

    public void OnClientConnected(NetEndPoint remoteEP)
    {
        Console.WriteLine("Client " + remoteEP.TCPEndPoint + " Connected");

        using (NetPacket packet = new NetPacket(listener.System, TransportProtocol.TCP))
        {
            NetStruct netStruct = new NetStruct();
            netStruct.IntValue = 10;
            netStruct.StringValue = "Hello World!";
            netStruct.FloatValue = 3.14f;

            NetClass netClass = new NetClass();
            netClass.IntValue = 20;
            netClass.StringValue = "Goodbye World!";
            netClass.SetFloatValue(new float[] { 1.0f, 2.0f, 3.0f });
            netClass.NetStructValue = netStruct;

            packet.SerializeClass(netClass);
            remoteEP.Send(packet, TransportProtocol.TCP);
        }
    }

    public void OnClientDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect)
    {
        Console.WriteLine("Client " + remoteEP.TCPEndPoint + " Disconnected: " + disconnect.DisconnectCode.ToString() + (disconnect.DisconnectData != null ? " - " + disconnect.DisconnectData.ReadString() : ""));
    }

    public void OnPacketReceived(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol)
    {
        NetClass netClass = packet.DeserializeClass<NetClass>();
        Console.WriteLine("Packet Received from " + remoteEP.TCPEndPoint + " with class: " + netClass.IntValue + ", " + netClass.StringValue + ", (" + netClass.GetFloatValue()![0] + ", " + netClass.GetFloatValue()![1] + ", " + netClass.GetFloatValue()![2] + ")" + ", struct(" + netClass.NetStructValue.IntValue + ", " + netClass.NetStructValue.StringValue + ", " + netClass.NetStructValue.FloatValue + ")");
        packetCount++;

        using (NetPacket respPacket = new NetPacket(listener.System, TransportProtocol.TCP))
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
            remoteEP.Send(respPacket, TransportProtocol.TCP);
        }
    }

    public void OnNetworkError(NetEndPoint? remoteEP, SocketError error)
    {
        Console.WriteLine("Error: " + error.ToString());
    }

    // Main Method
    static void Main(string[] args)
    {
        Server.Instance.Start(7777, "CNetTest");
    }
}
