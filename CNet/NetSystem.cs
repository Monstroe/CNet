using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CNet
{
    /// <summary>
    /// Represents a network system. This class is the main class for all network operations.
    /// </summary>
    public class NetSystem : IDisposable
    {
        public delegate void ConnectionRequestHandler(NetRequest request);
        public delegate void ConnectedHandler(NetEndPoint remoteEP);
        public delegate void DisconnectedHandler(NetEndPoint remoteEP, NetDisconnect disconnect);
        public delegate void PacketReceiveHandler(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol);
        public delegate void NetworkErrorHandler(NetEndPoint? remoteEP, SocketError error);

        public event ConnectionRequestHandler? OnConnectionRequest;
        public event ConnectedHandler? OnConnected;
        public event DisconnectedHandler? OnDisconnected;
        public event PacketReceiveHandler? OnPacketReceive;
        public event NetworkErrorHandler? OnNetworkError;

        /// <summary>
        /// Gets the address of the system.
        /// </summary>
        /// <remarks>
        /// If the system is a client, this is the server's address. If the system is a listener, this is the listener's address.
        /// </remarks>
        public string? Address { get; private set; }

        /// <summary>
        /// Gets the port of the system.
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the system is connected.
        /// </summary>
        /// <remarks>
        /// This property is only used when the system is a client.
        /// </remarks>
        public bool IsConnected { get { return tcpSocket?.Connected ?? false; } }

        /// <summary>
        /// Gets the mode of the system.
        /// </summary>
        public SystemMode Mode { get; private set; }

        /// <summary>
        /// Gets the settings for the TCP protocol.
        /// </summary>
        public ProtocolSettings TCP { get; }

        /// <summary>
        /// Gets the settings for the UDP protocol.
        /// </summary>
        public ProtocolSettings UDP { get; }

        /// <summary>
        /// Gets or sets the maximum number of pending connections that can be queued.
        /// </summary>
        public int MaxPendingConnections { get; set; }

        /// <summary>
        /// Gets the serialize manager for handling serialization and deserialization of objects.
        /// </summary>
        public SerializeManager Serializer { get; }

        /// <summary>
        /// Gets the local end point.
        /// </summary>
        public NetEndPoint? LocalEndPoint { get; private set; }

        /// <summary>
        /// Gets the remote end point.
        /// </summary>
        /// <remarks>
        /// This property is only used when the system is a client.
        /// </remarks>
        public NetEndPoint? RemoteEndPoint
        {
            get
            {
                if (connectionsTCP.Count == 1)
                    return connectionsTCP.Values.ToList().First();
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the remote end points.
        /// </summary>
        /// <remarks>
        /// This property is only used when the system is a listener.
        /// </remarks>
        public List<NetEndPoint>? RemoteEndPoints
        {
            get
            {
                if (connectionsTCP.Count > 1)
                    return connectionsTCP.Values.ToList();
                else
                    return null;
            }
        }

        internal ArrayPool<byte> PacketPool { get; private set; }

        private readonly ConcurrentDictionary<IPEndPoint, NetEndPoint> connectionsTCP;
        private readonly ConcurrentDictionary<IPEndPoint, NetEndPoint> connectionsUDP;

        private readonly ConcurrentDictionary<ulong, NetConnect> connectingClients;

        private readonly ConcurrentQueue<NetEndPoint> beginReceiveQueue;
        private readonly HashSet<uint> freeIds;
        private int nextClientId;

        private Socket? tcpSocket;
        private Socket? udpSocket;
        private bool systemStarted;
        private CancellationTokenSource mainCancelTokenSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetSystem"/> class.
        /// </summary>
        public NetSystem()
        {
            Mode = SystemMode.None;

            TCP = new ProtocolSettings
            {
                HEARTBEAT_INTERVAL = 1000,
                CONNECTION_TIMEOUT = 15000,
                SOCKET_RECEIVE_BUFFER_SIZE = 0,
                SOCKET_SEND_BUFFER_SIZE = 0,
                MAX_PACKET_SIZE = 1024
            };

            UDP = new ProtocolSettings
            {
                HEARTBEAT_INTERVAL = 500,
                CONNECTION_TIMEOUT = 15000,
                SOCKET_RECEIVE_BUFFER_SIZE = 0,
                SOCKET_SEND_BUFFER_SIZE = 0,
                MAX_PACKET_SIZE = 1024
            };

            Serializer = new SerializeManager();
            PacketPool = ArrayPool<byte>.Shared;

            connectionsTCP = new ConcurrentDictionary<IPEndPoint, NetEndPoint>();
            connectionsUDP = new ConcurrentDictionary<IPEndPoint, NetEndPoint>();
            connectingClients = new ConcurrentDictionary<ulong, NetConnect>();
            beginReceiveQueue = new ConcurrentQueue<NetEndPoint>();
            freeIds = new HashSet<uint>();
            nextClientId = 0;
            systemStarted = false;
            mainCancelTokenSource = new CancellationTokenSource();
        }

        private void Init()
        {
            IPEndPoint ep = new IPEndPoint(Address == null ? IPAddress.Any : IPAddress.Parse(Address), Port);
            tcpSocket = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            if (TCP.SOCKET_RECEIVE_BUFFER_SIZE > 0)
            {
                tcpSocket.ReceiveBufferSize = TCP.SOCKET_RECEIVE_BUFFER_SIZE;
            }
            if (TCP.SOCKET_SEND_BUFFER_SIZE > 0)
            {
                tcpSocket.SendBufferSize = TCP.SOCKET_SEND_BUFFER_SIZE;
            }

            udpSocket = new Socket(ep.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            if (UDP.SOCKET_RECEIVE_BUFFER_SIZE > 0)
            {
                udpSocket.ReceiveBufferSize = UDP.SOCKET_RECEIVE_BUFFER_SIZE;
            }
            if (UDP.SOCKET_SEND_BUFFER_SIZE > 0)
            {
                udpSocket.SendBufferSize = UDP.SOCKET_SEND_BUFFER_SIZE;
            }

            LocalEndPoint = new NetEndPoint((IPEndPoint)tcpSocket.LocalEndPoint!, (IPEndPoint)udpSocket.LocalEndPoint!, tcpSocket, 0, this);
        }

        /// <summary>
        /// Registers an interface to receive network events.
        /// </summary>
        /// <param name="iClient">The client interface to register.</param>
        public void RegisterInterface(IEventNetClient iClient)
        {
            OnConnected += iClient.OnConnected;
            OnDisconnected += iClient.OnDisconnected;
            OnPacketReceive += iClient.OnPacketReceived;
            OnNetworkError += iClient.OnNetworkError;
        }

        /// <summary>
        /// Registers an interface to receive network events.
        /// </summary>
        /// <param name="iListener">The listener interface to register.</param>
        public void RegisterInterface(IEventNetListener iListener)
        {
            OnConnectionRequest += iListener.OnConnectionRequest;
            OnConnected += iListener.OnClientConnected;
            OnDisconnected += iListener.OnClientDisconnected;
            OnPacketReceive += iListener.OnPacketReceived;
            OnNetworkError += iListener.OnNetworkError;
        }

        /// <summary>
        /// Connects to the server.
        /// </summary>
        public async void Connect(string address, int port, string connectionKey)
        {
            // Checks to prevent method from being called again after system is connected or if the system is in Listener mode
            if (IsConnected || Mode == SystemMode.Listener)
            {
                throw new InvalidOperationException("Please call 'Close' before calling 'Connect' again.");
            }

            if (systemStarted)
            {
                systemStarted = false;
                tcpSocket?.Dispose();
                udpSocket?.Dispose();
                LocalEndPoint = null;
            }

            Address = address;
            Port = port;
            Init();
            Mode = SystemMode.Client;

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(Address), Port);
            NetEndPoint remoteEP = new NetEndPoint(endPoint, endPoint, tcpSocket!, 0, this);

            bool tcpConnected = false;
            bool udpConnected = false;

            await Task.Run(async () =>
            {
                try
                {
                    await tcpSocket!.ConnectAsync(Address, Port);
                    tcpConnected = true;
                }
                catch (SocketException ex) { ThrowErrorOnMainThread(remoteEP, ex.SocketErrorCode); }

                try
                {
                    udpSocket?.Connect(Address, Port);
                    udpConnected = true;
                }
                catch (SocketException ex) { ThrowErrorOnMainThread(remoteEP, ex.SocketErrorCode); }

                systemStarted = true;
                if (!tcpConnected || !udpConnected)
                {
                    mainCancelTokenSource.Cancel();
                    return;
                }

                try
                {
                    var (keyPacket, validKeyPacket) = await ReceiveTCPAsync(remoteEP, TCP.CONNECTION_TIMEOUT);
                    if (keyPacket.UnreadLength < sizeof(int))
                    {
                        DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.InvalidPacket, null, null), false, false);
                        return;
                    }

                    int keyPacketConnectionCode = keyPacket.ReadInt();
                    if (keyPacketConnectionCode == (int)DisconnectionCode.ConnectionDenied)
                    {
                        DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.ConnectionDenied, null, null), false, false);
                        return;
                    }
                    if (keyPacketConnectionCode != (int)ConnectionCode.ConnectionKeyRequest)
                    {
                        DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.InvalidPacket, null, null), false, false);
                        return;
                    }

                    keyPacket.Dispose();

                    using (NetPacket connectionPacket = new NetPacket(this, TransportProtocol.TCP))
                    {
                        connectionPacket.Write((int)ConnectionCode.ConnectionKey);
                        connectionPacket.Write(connectionKey);
                        bool success = await SendInternal(remoteEP, connectionPacket, TransportProtocol.TCP, false, true, true, false); // <-- false because we dispose the packet here
                        if (!success) // Send Internal already handles the disconnect
                        {
                            return;
                        }
                    }

                    var (tokenPacket, validTokenPacket) = await ReceiveTCPAsync(remoteEP, TCP.CONNECTION_TIMEOUT);
                    if (tokenPacket.UnreadLength < sizeof(int) || !validTokenPacket)
                    {
                        DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.InvalidPacket, null, null), false, false);
                        return;
                    }

                    int tokenPacketConnectionCode = tokenPacket.ReadInt();
                    if (tokenPacketConnectionCode != (int)ConnectionCode.ConnectionTokenRequest)
                    {
                        DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.InvalidPacket, null, null), false, false);
                        return;
                    }

                    if (tokenPacket.UnreadLength != sizeof(ulong))
                    {
                        DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.InvalidPacket, null, null), false, false);
                        return;
                    }

                    ulong token = tokenPacket.ReadULong();
                    tokenPacket.Dispose();
                    bool stopSendingUdp = false;
                    _ = Task.Run(async () =>
                    {
                        while (!mainCancelTokenSource.IsCancellationRequested && !stopSendingUdp)
                        {
                            using (NetPacket udpPacket = new NetPacket(this, TransportProtocol.UDP, 0))
                            {
                                udpPacket.Write((int)ConnectionCode.ConnectionToken);
                                udpPacket.Write(token);
                                SendOnMainThread(remoteEP, udpPacket, TransportProtocol.UDP, false, true, false);
                            }

                            await Task.Delay(UDP.HEARTBEAT_INTERVAL, mainCancelTokenSource.Token); // <--- This is why the OperationCanceledException is caught
                        }
                    }, mainCancelTokenSource.Token);

                    var (acceptedPacket, validAcceptedPacket) = await ReceiveTCPAsync(remoteEP, TCP.CONNECTION_TIMEOUT);
                    stopSendingUdp = true;

                    if (acceptedPacket.UnreadLength < sizeof(int) || !validAcceptedPacket)
                    {
                        DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.InvalidPacket, null, null), false, false);
                        return;
                    }

                    int acceptedPacketConnectionCode = acceptedPacket.ReadInt();
                    if (acceptedPacketConnectionCode != (int)ConnectionCode.ConnectionAccepted)
                    {
                        DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.InvalidPacket, null, null), false, false);
                        return;
                    }

                    if (acceptedPacket.UnreadLength != sizeof(uint))
                    {
                        DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.InvalidPacket, null, null), false, false);
                        return;
                    }

                    uint newId = acceptedPacket.ReadUInt();
                    acceptedPacket.Dispose();
                    LocalEndPoint!.ID = newId;

                    if (!connectionsTCP.TryAdd(endPoint, remoteEP))
                    {
                        throw new Exception("Failed to add NetEndPoint to connectionsTCP");
                    }
                    if (!connectionsUDP.TryAdd(endPoint, remoteEP))
                    {
                        throw new Exception("Failed to add NetEndPoint to connectionsUDP");
                    }

                    ConnectOnMainThread(remoteEP);
                    StartReceiving();
                    StartHeartbeats();
                    beginReceiveQueue.Enqueue(remoteEP);
                }
                catch (SocketException ex)
                {
                    DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.SocketError, null, ex.SocketErrorCode), false, false);
                    mainCancelTokenSource.Cancel();
                }
                catch (ObjectDisposedException) { }
                catch (OperationCanceledException) { }
            }, mainCancelTokenSource.Token);
        }

        private void ConnectOnMainThread(NetEndPoint remoteEP)
        {
            ThreadManager.ExecuteOnMainThread(() => ConnectInternal(remoteEP));
        }

        private void ConnectInternal(NetEndPoint remoteEP)
        {
            OnConnected?.Invoke(remoteEP);
        }

        /// <summary>
        /// Listens for incoming connections.
        /// </summary>
        public void Listen(int port)
        {
            // Checks to prevent method from being called twice
            if (systemStarted)
            {
                throw new InvalidOperationException("Please call 'Close' before calling 'Listen' again.");
            }

            try
            {
                Port = port;
                Init();
                Mode = SystemMode.Listener;

                IPEndPoint ep = new IPEndPoint(IPAddress.Any, Port);
                tcpSocket!.Bind(ep);
                udpSocket!.Bind(ep);

                tcpSocket!.Listen(MaxPendingConnections);

                LocalEndPoint = new NetEndPoint((IPEndPoint)tcpSocket.LocalEndPoint!, (IPEndPoint)udpSocket.LocalEndPoint!, tcpSocket, 0, this);
                systemStarted = true;

                AcceptClients();
                StartReceiving();
                StartHeartbeats();
                TokenRemoval();
            }
            catch (SocketException ex)
            {
                ThrowErrorOnMainThread(null, ex.SocketErrorCode);
                mainCancelTokenSource.Cancel();
            }
            catch (ObjectDisposedException) { }
        }

        private void AcceptClients()
        {
            Task.Run(async () =>
            {
                while (!mainCancelTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        Socket clientTcpSock = await tcpSocket!.AcceptAsync();
                        EndPoint? clientEndPoint = clientTcpSock.RemoteEndPoint;
                        if (clientEndPoint != null)
                        {
                            uint newID = GenerateClientID();
                            NetEndPoint clientEP = new NetEndPoint((IPEndPoint)clientEndPoint, null, clientTcpSock, newID, this);
                            NetRequest request = new NetRequest(clientEP, this);
                            HandleConnectionRequestOnMainThread(request);
                        }
                        else
                        {
                            clientTcpSock.Dispose();
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.OperationAborted)
                        {
                            ThrowErrorOnMainThread(null, ex.SocketErrorCode);
                        }
                    }
                    catch (ObjectDisposedException) { }
                }
            }, mainCancelTokenSource.Token);
        }

        private void HandleConnectionRequestOnMainThread(NetRequest request)
        {
            ThreadManager.ExecuteOnMainThread(() => HandleConnectionRequestInternal(request));
        }

        private void HandleConnectionRequestInternal(NetRequest request)
        {
            OnConnectionRequest?.Invoke(request);
        }

        internal void HandleConnectionResult(bool result, NetEndPoint remoteEP, string? connectionKey)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (result && connectionKey != null)
                    {
                        using (NetPacket packet = new NetPacket(this, TransportProtocol.TCP))
                        {
                            packet.Write((int)ConnectionCode.ConnectionKeyRequest);
                            SendOnMainThread(remoteEP, packet, TransportProtocol.TCP, false, true, true);
                        }

                        var (connectionKeyPacket, validPacket) = await ReceiveTCPAsync(remoteEP, TCP.CONNECTION_TIMEOUT);
                        if (connectionKeyPacket.UnreadLength < sizeof(int) || !validPacket)
                        {
                            DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.InvalidPacket, null, null), false, false);
                            return;
                        }

                        int connectionCode = connectionKeyPacket.ReadInt();
                        if (connectionCode == (int)ConnectionCode.ConnectionKey)
                        {
                            if (connectionKeyPacket.UnreadLength != sizeof(int) + Encoding.UTF8.GetByteCount(connectionKey))
                            {
                                DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.InvalidPacket, null, null), false, false);
                                return;
                            }

                            string receivedKey = connectionKeyPacket.ReadString();
                            connectionKeyPacket.Dispose();
                            if (receivedKey == connectionKey)
                            {
                                ulong token = GenerateToken();

                                if (!connectingClients.TryAdd(token, new NetConnect(remoteEP, DateTime.UtcNow.AddMilliseconds(UDP.CONNECTION_TIMEOUT))))
                                {
                                    throw new Exception("Failed to add connection token to connectingClients");
                                }

                                using (NetPacket packet = new NetPacket(this, TransportProtocol.TCP))
                                {
                                    packet.Write((int)ConnectionCode.ConnectionTokenRequest);
                                    packet.Write(token);
                                    SendOnMainThread(remoteEP, packet, TransportProtocol.TCP, false, true, true);
                                }
                            }
                            else
                            {
                                DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.ConnectionDenied, null, null), false, true);
                            }
                        }
                        else
                        {
                            DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.InvalidPacket, null, null), false, false);
                        }
                    }
                    else
                    {
                        DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.ConnectionDenied, null, null), false, true);
                    }
                }
                catch (SocketException ex)
                {
                    DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.SocketError, null, ex.SocketErrorCode), false, false);
                }
                catch (ObjectDisposedException) { }
            }, mainCancelTokenSource.Token);
        }

        /// <summary>
        /// Sends a network packet using the specified protocol.
        /// </summary>
        /// <param name="remoteEP">The remote end point to send the packet to.</param>
        /// <param name="packet">The network packet to send.</param>
        /// <param name="protocol">The protocol to use for sending the packet.</param>
        public void Send(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol)
        {
            if (packet.Length > (protocol == TransportProtocol.TCP ? TCP.MAX_PACKET_SIZE : UDP.MAX_PACKET_SIZE))
            {
                throw new Exception("Packets cannot be larger than " + (protocol == TransportProtocol.TCP ? "TCP." : "UDP.") + "MaxPacketSize.");
            }

            SendOnMainThread(remoteEP, packet, protocol, true, true, true);
        }

        private void SendOnMainThread(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol, bool checkConnected, bool disconnectOnError, bool insertLength)
        {
            if (packet.Protocol != protocol)
            {
                throw new Exception("Packet protocols do not match.");
            }

            NetPacket sendPacket = new NetPacket(this, protocol);
            sendPacket.SetBytes(packet.ByteSegment);

            if (protocol == TransportProtocol.TCP)
            {
                remoteEP.TCPHeartbeatInterval = 0;
            }
            else
            {
                remoteEP.UDPHeartbeatInterval = 0;
            }

            ThreadManager.ExecuteOnMainThread(async () => await SendInternal(remoteEP, sendPacket, protocol, checkConnected, disconnectOnError, insertLength, true));
        }

        private async Task<bool> SendInternal(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol, bool checkConnected, bool disconnectOnError, bool insertLength, bool disposePacket)
        {
            bool returnValue = false;

            if (!checkConnected || (connectionsTCP.ContainsKey(remoteEP.TCPEndPoint!) && connectionsUDP.ContainsKey(remoteEP.UDPEndPoint!)))
            {
                if (insertLength)
                {
                    packet.SetLength();
                    packet.StartIndex = 0;
                }

                try
                {
                    if (protocol == TransportProtocol.TCP)
                    {
                        await remoteEP.TCPSocket.SendAsync(packet.ByteSegment, SocketFlags.None);
                        returnValue = true;
                    }
                    else
                    {
                        await udpSocket!.SendToAsync(packet.ByteSegment, SocketFlags.None, remoteEP.UDPEndPoint!);
                        returnValue = true;
                    }
                }
                catch (SocketException ex)
                {
                    if (disconnectOnError)
                    {
                        await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectionCode.SocketError, null, ex.SocketErrorCode), checkConnected, false);
                    }
                    else
                    {
                        ThrowErrorInternal(remoteEP, ex.SocketErrorCode);
                    }
                }
                catch (ObjectDisposedException) { }
            }

            if (disposePacket)
            {
                packet.Dispose();
            }

            return returnValue;
        }

        /// <summary>
        /// Disconnects from the network with a specified disconnect packet.
        /// </summary>
        /// <param name="remoteEP">The remote end point to disconnect from.</param>
        /// <param name="disconnectPacket">The disconnect packet to send.</param>
        public void Disconnect(NetEndPoint remoteEP, NetPacket? disconnectPacket = null)
        {
            if (disconnectPacket == null)
            {
                DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.ConnectionClosed, null, null), true, true);
                return;
            }

            if (disconnectPacket.Protocol != TransportProtocol.TCP)
            {
                throw new Exception("Packet protocol for disconnect packets must be TransportProtocol.TCP.");
            }

            NetPacket sendPacket = new NetPacket(this, TransportProtocol.TCP);
            sendPacket.SetBytes(disconnectPacket.ByteSegment);
            DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.ConnectionClosedWithMessage, sendPacket, null), true, true);
        }

        /// <summary>
        /// Disconnects from the network forcefully.
        /// </summary>
        /// <param name="remoteEP">The remote end point to disconnect from.</param>
        public async void DisconnectForcefully(NetEndPoint remoteEP)
        {
            await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectionCode.ConnectionClosedForcefully, null, null), true, false);
        }

        private void DisconnectOnMainThread(NetEndPoint remoteEP, NetDisconnect disconnect, bool checkConnected, bool sendDisconnectPacketToRemote)
        {
            ThreadManager.ExecuteOnMainThread(async () => await DisconnectInternal(remoteEP, disconnect, checkConnected, sendDisconnectPacketToRemote));
        }

        private async Task<bool> DisconnectInternal(NetEndPoint remoteEP, NetDisconnect disconnect, bool checkConnected, bool sendDisconnectPacketToRemote)
        {
            bool returnValue = false;

            if (!checkConnected || (connectionsTCP.ContainsKey(remoteEP.TCPEndPoint!) && connectionsUDP.ContainsKey(remoteEP.UDPEndPoint!)))
            {
                if (sendDisconnectPacketToRemote)
                {
                    using (NetPacket packet = new NetPacket(this, TransportProtocol.TCP, 0))
                    {
                        packet.Write((int)disconnect.DisconnectCode);
                        if (disconnect.DisconnectData != null)
                        {
                            disconnect.DisconnectData.SetLength();
                            disconnect.DisconnectData.StartIndex = 0;
                            // The length integer is already written in the disconnect packet (from the Disconnect method)
                            packet.SetBytes(disconnect.DisconnectData.ByteSegment);
                            // Set the start index back to sizeof(int) for when OnDisconnected is invoked
                            disconnect.DisconnectData.StartIndex = sizeof(int);
                        }
                        returnValue = await SendInternal(remoteEP, packet, TransportProtocol.TCP, checkConnected, false, false, false);
                    }
                }

                returnValue = CloseRemote(remoteEP);
                if (returnValue)
                {
                    OnDisconnected?.Invoke(remoteEP, disconnect);
                }
            }

            disconnect.DisconnectData?.Dispose();

            return returnValue;
        }

        /// <summary>
        /// Closes the network system.
        /// </summary>
        /// <param name="sendDisconnectPacketToRemote">Whether to send a disconnect packet to the remote end point(s).</param>
        public void Close(bool sendDisconnectPacketToRemote)
        {
            ThreadManager.ExecuteOnMainThread(async () =>
            {
                foreach (var remoteEP in connectionsTCP.Values)
                {
                    await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectionCode.ConnectionClosed, null, null), true, sendDisconnectPacketToRemote);
                }

                Dispose();
            });
        }

        private bool CloseRemote(NetEndPoint remoteEP)
        {
            if (remoteEP.TCPSocket.Connected)
            {
                try
                {
                    remoteEP.TCPSocket.Shutdown(SocketShutdown.Send);
                    remoteEP.TCPSocket.Close();
                    if (remoteEP.TCPEndPoint != null)
                    {
                        connectionsTCP.TryRemove(remoteEP.TCPEndPoint, out _);
                    }

                    if (remoteEP.UDPEndPoint != null)
                    {
                        connectionsUDP.TryRemove(remoteEP.UDPEndPoint, out _);
                    }

                    lock (freeIds)
                    {
                        freeIds.Add(remoteEP.ID);
                    }

                    return true;
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }

            return false;
        }

        /// <summary>
        /// Polls all pending events.
        /// </summary>
        /// <remarks>
        /// This method should be called in an update loop. It will receive all pending events on the main thread.
        /// </remarks>
        public void Update()
        {
            ThreadManager.PollMainThread();
        }

        private void StartReceiving()
        {
            Task.Run(() =>
            {
                while (!mainCancelTokenSource.IsCancellationRequested)
                {
                    if (beginReceiveQueue.TryDequeue(out var netEndPoint))
                    {
                        StartReceivingTCP(netEndPoint);
                    }
                }
            }, mainCancelTokenSource.Token);
            StartReceivingUDP();
        }

        private void StartHeartbeats()
        {
            TCPHeartbeat();
            UDPHeartbeat();
        }

        private void StartReceivingTCP(NetEndPoint netEndPoint)
        {
            Task.Run(async () =>
            {
                bool disconnect = false;
                while (!mainCancelTokenSource.IsCancellationRequested && !disconnect)
                {
                    using (NetPacket bufferPacket = new NetPacket(this, TransportProtocol.TCP, 0))
                    {
                        try
                        {
                            NetPacket finalPacket;
                            bool validPacket;

                            (finalPacket, validPacket) = await ReceiveTCPAsync(netEndPoint, bufferPacket);

                            if (!validPacket && finalPacket.Length > 0)
                            {
                                await ProcessStatusPacket(finalPacket, netEndPoint, bufferPacket);
                                disconnect = true;
                            }

                            if (validPacket)
                            {
                                ReceivePacketOnMainThread(netEndPoint, finalPacket, TransportProtocol.TCP);
                            }
                        }
                        catch (SocketException ex)
                        {
                            DisconnectOnMainThread(netEndPoint, new NetDisconnect(DisconnectionCode.SocketError, null, ex.SocketErrorCode), true, false);
                            disconnect = true;
                        }
                        catch (ObjectDisposedException) { }
                    }
                }
            }, mainCancelTokenSource.Token);
        }

        private Task<(NetPacket, bool)> ReceiveTCPAsync(NetEndPoint netEndPoint, NetPacket bufferPacket)
        {
            NetPacket finalPacket = new NetPacket(this, TransportProtocol.TCP);
            int? expectedLength = null;
            bool grabbedPacketLength = false;
            bool validPacket = false;

            while (!mainCancelTokenSource.IsCancellationRequested)
            {
                // If there are no more bytes already in bufferPacket, receive more
                int receivedBytes = netEndPoint.TCPSocket.Receive(bufferPacket.ByteArray, bufferPacket.Length, expectedLength.HasValue ? expectedLength.Value - bufferPacket.UnreadLength : sizeof(int), SocketFlags.None);
                // If a completely blank packet was sent
                if (receivedBytes == 0)
                {
                    finalPacket.Write((int)DisconnectionCode.ConnectionClosedForcefully);
                    break;
                }
                bufferPacket.Length += receivedBytes;

                // If there are enough bytes to form an int
                if (bufferPacket.Length >= sizeof(int))
                {
                    // If we haven't already grabbed the packet length
                    if (!grabbedPacketLength)
                    {
                        expectedLength = bufferPacket.ReadInt();

                        // Connection status packets have an expected length of less than 0, so just return the final packet so it can be acted upon later
                        if (expectedLength < 0)
                        {
                            finalPacket.Write(expectedLength.Value);
                            break;
                        }
                        // If the expected length of the packet is greater than the set max packet size
                        if (expectedLength > TCP.MAX_PACKET_SIZE)
                        {
                            finalPacket.Write((int)DisconnectionCode.PacketOverMaxSize);
                            break;
                        }

                        // Reset the timeout time for the remote end point (since we received a packet)
                        netEndPoint.TCPConnectionTimeoutTime = 0;
                        // If the expected length of the packet is 0, this is a heartbeat packet
                        if (expectedLength == (int)ConnectionCode.Heartbeat)
                        {
                            break;
                        }

                        grabbedPacketLength = true;
                    }

                    // If all the bytes in the packet have been received
                    if (bufferPacket.UnreadLength == expectedLength)
                    {
                        finalPacket.SetBytes(bufferPacket.GetBytes(expectedLength.Value));
                        validPacket = true;
                        break;
                    }
                }
            }

            return Task.FromResult((finalPacket, validPacket));
        }

        private async Task<(NetPacket, bool)> ReceiveTCPAsync(NetEndPoint remoteEP, int timeout, NetPacket? bufferPacket = null)
        {
            NetPacket packet = bufferPacket ?? new NetPacket(this, TransportProtocol.TCP, 0);
            remoteEP.TCPSocket.ReceiveTimeout = timeout;
            var result = await ReceiveTCPAsync(remoteEP, packet);
            remoteEP.TCPSocket.ReceiveTimeout = 0;
            return result;
        }

        private void TCPHeartbeat()
        {
            Task.Run(async () =>
            {
                try
                {
                    int checkInterval = TCP.HEARTBEAT_INTERVAL / 5;
                    DateTime lastCheckTime = DateTime.UtcNow;
                    while (!mainCancelTokenSource.IsCancellationRequested)
                    {
                        double accumulatedTime = (float)(DateTime.UtcNow - lastCheckTime).TotalMilliseconds;
                        lastCheckTime = DateTime.UtcNow;
                        foreach (var remoteEP in connectionsTCP.Values)
                        {
                            if (remoteEP.TCPConnectionTimeoutTime >= TCP.CONNECTION_TIMEOUT)
                            {
                                DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.ConnectionLost, null, null), true, false);
                                break;
                            }

                            if (remoteEP.TCPHeartbeatInterval >= TCP.HEARTBEAT_INTERVAL)
                            {
                                using (NetPacket packet = new NetPacket(this, TransportProtocol.TCP, 0))
                                {
                                    packet.Write((int)ConnectionCode.Heartbeat);
                                    SendOnMainThread(remoteEP, packet, TransportProtocol.TCP, true, true, false);
                                }
                            }

                            remoteEP.TCPConnectionTimeoutTime += accumulatedTime;
                            remoteEP.TCPHeartbeatInterval += accumulatedTime;
                        }
                        await Task.Delay(checkInterval, mainCancelTokenSource.Token); // <--- This is why the OperationCanceledException is caught
                    }
                }
                catch (OperationCanceledException) { }
            }, mainCancelTokenSource.Token);
        }

        private void StartReceivingUDP()
        {
            Task.Run(async () =>
            {
                while (!mainCancelTokenSource.IsCancellationRequested)
                {
                    using (NetPacket bufferPacket = new NetPacket(this, TransportProtocol.UDP, 0))
                    {
                        NetEndPoint? netEndPoint = null;
                        try
                        {
                            bool validPacket;
                            NetPacket finalPacket;

                            (finalPacket, netEndPoint, validPacket) = await ReceiveUDPAsync(bufferPacket);

                            if (!validPacket && finalPacket.Length > 0 && netEndPoint != null)
                            {
                                await ProcessStatusPacket(finalPacket, netEndPoint, null);
                            }

                            if (validPacket)
                            {
                                ReceivePacketOnMainThread(netEndPoint!, finalPacket, TransportProtocol.UDP);
                            }
                        }
                        catch (SocketException ex)
                        {
                            if (netEndPoint != null)
                            {
                                DisconnectOnMainThread(netEndPoint, new NetDisconnect(DisconnectionCode.SocketError, null, ex.SocketErrorCode), true, false);
                            }
                            else
                            {
                                if (ex.SocketErrorCode != SocketError.Interrupted)
                                {
                                    ThrowErrorOnMainThread(null, ex.SocketErrorCode);
                                }
                            }
                        }
                        catch (ObjectDisposedException) { }
                    }
                }
            }, mainCancelTokenSource.Token);
        }

        private Task<(NetPacket, NetEndPoint?, bool)> ReceiveUDPAsync(NetPacket bufferPacket)
        {
            NetPacket finalPacket = new NetPacket(this, TransportProtocol.UDP);
            NetEndPoint? remoteEP = null;

            bool validPacket = false;

            while (!mainCancelTokenSource.IsCancellationRequested)
            {
                EndPoint unknownEndPoint = new IPEndPoint(IPAddress.Any, Port);
                int receivedBytes = udpSocket!.ReceiveFrom(bufferPacket.ByteArray, 0, bufferPacket.ByteArray.Length, SocketFlags.None, ref unknownEndPoint);
                bufferPacket.Length += receivedBytes;

                // Disregard packet as it is too small
                if (receivedBytes < sizeof(int))
                {
                    break;
                }

                int expectedLength = bufferPacket.ReadInt();

                // If the received data is from a currently connected end point
                if (connectionsUDP.TryGetValue((IPEndPoint)unknownEndPoint, out remoteEP))
                {
                    // Packets expected length was larger than the actual amount of bytes received AKA partial packet loss, disregard
                    if (expectedLength > receivedBytes - sizeof(int))
                    {
                        break;
                    }

                    // If another connection token packet was received from an connected client, disregard
                    if (expectedLength == (int)ConnectionCode.ConnectionToken)
                    {
                        break;
                    }

                    // Packets expected length was smaller than the actual amount of bytes received
                    if (expectedLength < receivedBytes - sizeof(int))
                    {
                        finalPacket.Write((int)DisconnectionCode.InvalidPacket);
                        break;
                    }
                    // If the expected length of the packet is greater than the set max packet size
                    if (expectedLength > UDP.MAX_PACKET_SIZE)
                    {
                        finalPacket.Write((int)DisconnectionCode.PacketOverMaxSize);
                        break;
                    }

                    // Reset the timeout time for the remote end point (since we received a packet)
                    remoteEP.UDPConnectionTimeoutTime = 0;
                    // If the expected length of the packet is 0, this is a heartbeat packet
                    if (expectedLength == (int)ConnectionCode.Heartbeat)
                    {
                        break;
                    }

                    finalPacket.SetBytes(bufferPacket.GetBytes(expectedLength));
                    validPacket = true;
                }
                else
                {
                    // If the received data is from an unknown end point and it's a connection token packet, read the token and add the user to the connectionsTCP and connectionsUDP dictionary
                    if (expectedLength == (int)ConnectionCode.ConnectionToken)
                    {
                        if (bufferPacket.UnreadLength != sizeof(ulong))
                        {
                            break;
                        }

                        ulong token = bufferPacket.ReadULong();
                        if (connectingClients.TryRemove(token, out var netConnect))
                        {
                            // If the connection token has expired
                            if (DateTime.UtcNow > netConnect.ConnectionTokenExpiry)
                            {
                                break;
                            }

                            netConnect.ConnectingEP.UDPEndPoint = (IPEndPoint)unknownEndPoint;

                            if (!connectionsTCP.TryAdd(netConnect.ConnectingEP.TCPEndPoint!, netConnect.ConnectingEP))
                            {
                                throw new Exception("Failed to add NetEndPoint to connectionsTCP");
                            }

                            if (!connectionsUDP.TryAdd(netConnect.ConnectingEP.UDPEndPoint!, netConnect.ConnectingEP))
                            {
                                throw new Exception("Failed to add NetEndPoint to connectionsUDP");
                            }

                            using (NetPacket packet = new NetPacket(this, TransportProtocol.TCP))
                            {
                                packet.Write((int)ConnectionCode.ConnectionAccepted);
                                packet.Write(netConnect.ConnectingEP.ID);
                                SendOnMainThread(netConnect.ConnectingEP, packet, TransportProtocol.TCP, true, true, true);
                            }

                            beginReceiveQueue.Enqueue(netConnect.ConnectingEP);
                            ConnectOnMainThread(netConnect.ConnectingEP);
                        }
                    }
                }
                break;
            }

            return Task.FromResult((finalPacket, remoteEP, validPacket));
        }

        private void UDPHeartbeat()
        {
            Task.Run(async () =>
            {
                try
                {
                    int checkInterval = UDP.HEARTBEAT_INTERVAL / 5;
                    DateTime lastCheckTime = DateTime.UtcNow;
                    while (!mainCancelTokenSource.IsCancellationRequested)
                    {
                        double accumulatedTime = (float)(DateTime.UtcNow - lastCheckTime).TotalMilliseconds;
                        lastCheckTime = DateTime.UtcNow;
                        foreach (var remoteEP in connectionsUDP.Values)
                        {
                            if (remoteEP.UDPConnectionTimeoutTime >= UDP.CONNECTION_TIMEOUT)
                            {
                                DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectionCode.ConnectionLost, null, null), true, false);
                                break;
                            }

                            if (remoteEP.UDPHeartbeatInterval >= UDP.HEARTBEAT_INTERVAL)
                            {
                                using (NetPacket packet = new NetPacket(this, TransportProtocol.UDP, 0))
                                {
                                    packet.Write((int)ConnectionCode.Heartbeat);
                                    SendOnMainThread(remoteEP, packet, TransportProtocol.UDP, true, true, false);
                                }
                            }

                            remoteEP.UDPConnectionTimeoutTime += accumulatedTime;
                            remoteEP.UDPHeartbeatInterval += accumulatedTime;
                        }
                        await Task.Delay(checkInterval, mainCancelTokenSource.Token); // <--- This is why the OperationCanceledException is caught
                    }
                }
                catch (OperationCanceledException) { }
            }, mainCancelTokenSource.Token);
        }

        private void TokenRemoval()
        {
            Task.Run(async () =>
            {
                try
                {
                    int checkInterval = UDP.HEARTBEAT_INTERVAL / 5;
                    while (!mainCancelTokenSource.IsCancellationRequested)
                    {
                        foreach (var (token, netConnect) in connectingClients)
                        {
                            if (DateTime.UtcNow > netConnect.ConnectionTokenExpiry)
                            {
                                if (!connectingClients.TryRemove(token, out _))
                                {
                                    throw new Exception("Failed to remove expired connection token from connectingClients");
                                }

                                DisconnectOnMainThread(netConnect.ConnectingEP, new NetDisconnect(DisconnectionCode.ConnectionTimedOut, null, null), false, false);
                            }
                        }

                        await Task.Delay(checkInterval, mainCancelTokenSource.Token);
                    }

                }
                catch (OperationCanceledException) { }
            }, mainCancelTokenSource.Token);
        }

        private async Task ProcessStatusPacket(NetPacket finalPacket, NetEndPoint netEndPoint, NetPacket? bufferPacket)
        {
            int code = finalPacket.ReadInt();
            bool containsDisconnectCode = false;

            foreach (int c in Enum.GetValues(typeof(DisconnectionCode)))
            {
                if (c == code)
                {
                    containsDisconnectCode = true;
                    break;
                }
            }

            NetDisconnect disconnect;
            try
            {
                if (containsDisconnectCode)
                {
                    if (code == (int)DisconnectionCode.ConnectionClosedWithMessage && bufferPacket != null)
                    {
                        var (finalData, validPacket) = await ReceiveTCPAsync(netEndPoint, TCP.CONNECTION_TIMEOUT, bufferPacket);

                        if (validPacket)
                        {
                            disconnect = new NetDisconnect((DisconnectionCode)code, finalData, null);
                        }
                        else
                        {
                            disconnect = new NetDisconnect(DisconnectionCode.InvalidPacket, null, null);
                            finalData.Dispose();
                        }
                    }
                    else
                    {
                        disconnect = new NetDisconnect((DisconnectionCode)code, null, null);
                    }
                }
                else
                {
                    disconnect = new NetDisconnect(DisconnectionCode.InvalidPacket, null, null);
                }
            }
            catch (SocketException ex)
            {
                disconnect = new NetDisconnect(DisconnectionCode.SocketError, null, ex.SocketErrorCode);
            }

            DisconnectOnMainThread(netEndPoint, disconnect, true, false);
        }

        private void ReceivePacketOnMainThread(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol)
        {
            ThreadManager.ExecuteOnMainThread(() => ReceivePacketInternal(remoteEP, packet, protocol));
        }

        private void ReceivePacketInternal(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol)
        {
            OnPacketReceive?.Invoke(remoteEP, packet, protocol);
            packet.Dispose();
        }

        private void ThrowErrorOnMainThread(NetEndPoint? remoteEP, SocketError error)
        {
            ThreadManager.ExecuteOnMainThread(() => ThrowErrorInternal(remoteEP, error));
        }

        private void ThrowErrorInternal(NetEndPoint? remoteEP, SocketError error)
        {
            OnNetworkError?.Invoke(remoteEP, error);
        }

        private uint GenerateClientID()
        {
            lock (freeIds)
            {
                if (freeIds.Count > 0)
                {
                    uint recycledId = freeIds.First();
                    freeIds.Remove(recycledId);
                    return recycledId;
                }
            }

            return (uint)Interlocked.Increment(ref nextClientId);
        }

        private ulong GenerateToken()
        {
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);
            return BitConverter.ToUInt64(bytes);
        }

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    mainCancelTokenSource?.Cancel();

                    tcpSocket?.Dispose();
                    udpSocket?.Dispose();
                    mainCancelTokenSource?.Dispose();

                    connectionsTCP.Clear();
                    connectionsUDP.Clear();
                    beginReceiveQueue.Clear();
                    connectingClients.Clear();
                    freeIds.Clear();
                    nextClientId = 0;
                    Mode = SystemMode.None;
                    LocalEndPoint = null;
                    systemStarted = false;
                }

                disposed = true;
            }
        }

        ~NetSystem()
        {
            Dispose(false);
        }
    }
}
