using System.Net.Sockets;
using CNet;

namespace CNet_Tests;

public abstract class ClientNetworkTest : IEventNetClient
{
    public bool TestSuccess { get; protected set; } = false;
    public string? TestError { get; protected set; }
    public NetClient? Client { get; protected set; }

    protected CancellationTokenSource? cancellationToken;

    public virtual NetClient Start(string address, int port, string connectionKey, CancellationTokenSource token)
    {
        Client = new NetClient();
        Client.RegisterInterface(this);
        this.cancellationToken = token;
        Client.Connect(address, port, connectionKey);
        return Client;
    }

    public virtual void Stop(bool success, string? error = null)
    {
        TestSuccess = success;
        TestError = error;
        cancellationToken!.Cancel();
    }

    public virtual void Update()
    {
        Client?.Update();
    }

    public abstract void OnConnected(NetEndPoint remoteEP);
    public abstract void OnDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect);
    public abstract void OnNetworkError(NetEndPoint? remoteEP, SocketError error);
    public abstract void OnPacketReceived(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol);
}
