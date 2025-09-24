using System.Net.Sockets;
using CNet;

namespace CNet_Tests;

public abstract class ListenerNetworkTest : IEventNetListener
{
    public bool TestSuccess { get; protected set; } = false;
    public string? TestError { get; protected set; }
    public NetListener? Listener { get; protected set; }

    protected string connectionKey = string.Empty;
    protected CancellationTokenSource? cancellationToken;

    public virtual NetListener Start(int port, string connectionKey, CancellationTokenSource token)
    {
        Listener = new NetListener();
        Listener.RegisterInterface(this);
        this.connectionKey = connectionKey;
        this.cancellationToken = token;
        Listener.Listen(port);
        return Listener;
    }

    public virtual void Stop(bool success, string? error = null)
    {
        TestSuccess = success;
        TestError = error;
        cancellationToken!.Cancel();
    }

    public virtual void Update()
    {
        Listener?.Update();
    }

    public abstract void OnConnectionRequest(NetRequest request);
    public abstract void OnClientConnected(NetEndPoint remoteEP);
    public abstract void OnClientDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect);
    public abstract void OnNetworkError(NetEndPoint? remoteEP, SocketError error);
    public abstract void OnPacketReceived(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol);
}
