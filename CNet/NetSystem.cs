using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CNet
{
    public class NetSystem : IDisposable
    {
        enum RequestStatus
        {
            Accept = 1,
            Deny = 0
        }

        public delegate void ConnectionRequestHandler(NetRequest request);
        public delegate void ConnectedHandler(NetEndPoint remoteEndPoint);
        public delegate void DisconnectedHandler(NetEndPoint remoteEndPoint, NetDisconnect disconnect);
        public delegate void PacketReceiveHandler(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol);
        public delegate void NetworkErrorHandler(NetEndPoint remoteEndPoint, SocketException socketException);

        public event ConnectionRequestHandler OnConnectionRequest;
        public event ConnectedHandler OnConnected;
        public event DisconnectedHandler OnDisconnected;
        public event PacketReceiveHandler OnPacketReceive;
        public event NetworkErrorHandler OnNetworkError;

        public string Address { get; set; }
        public int Port { get; set; }
        public int MaxPendingConnections { get; set; }
        public bool IsConnected { get { return tcpSocket.Connected; } }

        public SystemMode Mode { get; private set; }

        public ProtocolSettings TCP { get; }
        public ProtocolSettings UDP { get; }

        public NetEndPoint RemoteEndPoint
        {
            get
            {
                if (connectionsTCP.Count == 1)
                    return connectionsTCP.Values.ToList().First();
                else
                    return null;
            }
        }

        public List<NetEndPoint> RemoteEndPoints
        {
            get { return connectionsTCP.Values.ToList(); }
        }

        private ConcurrentDictionary<IPEndPoint, NetEndPoint> connectionsTCP;
        private ConcurrentDictionary<IPEndPoint, NetEndPoint> connectionsUDP;

        private Socket tcpSocket;
        private Socket udpSocket;
        private bool systemConnected;
        private ConcurrentQueue<NetEndPoint> beginReceiveQueue;
        private CancellationTokenSource mainCancelTokenSource;
        private ArrayPool<byte> packetPool;

        public NetSystem()
        {
            MaxPendingConnections = 100;

            TCP = new ProtocolSettings();
            UDP = new ProtocolSettings();

            TCP.BufferSize = 1024;
            TCP.MaxPacketSize = 128;

            UDP.BufferSize = 4096;
            UDP.MaxPacketSize = 128;

            connectionsTCP = new ConcurrentDictionary<IPEndPoint, NetEndPoint>();
            connectionsUDP = new ConcurrentDictionary<IPEndPoint, NetEndPoint>();
            systemConnected = false;
            beginReceiveQueue = new ConcurrentQueue<NetEndPoint>();

            packetPool = ArrayPool<byte>.Shared;
        }

        public NetSystem(string address, int port) : this()
        {
            Address = address;
            Port = port;
        }

        public NetSystem(int port) : this()
        {
            Port = port;
        }

        private void InitFinalObjects()
        {
            IPEndPoint ep = new IPEndPoint(Address == null ? IPAddress.Any : IPAddress.Parse(Address), Port);

            mainCancelTokenSource = new CancellationTokenSource();

            tcpSocket = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            udpSocket = new Socket(ep.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        }

        public void RegisterInterface(IEventNetClient iClient)
        {
            OnConnected += iClient.OnConnected;
            OnDisconnected += iClient.OnDisconnected;
            OnPacketReceive += iClient.OnPacketReceived;
            OnNetworkError += iClient.OnNetworkError;
        }

        public void RegisterInterface(IEventNetListener iListener)
        {
            OnConnectionRequest += iListener.OnConnectionRequest;
            OnConnected += iListener.OnClientConnected;
            OnDisconnected += iListener.OnClientDisconnected;
            OnPacketReceive += iListener.OnPacketReceived;
            OnNetworkError += iListener.OnNetworkError;
        }

        public async void Connect()
        {
            // Checks to prevent method from being called twice
            if (systemConnected && tcpSocket != null && tcpSocket.Connected)
            {
                throw new InvalidOperationException("Please call 'Close' before calling 'Connect' again.");
            }

            InitFinalObjects();
            Mode = SystemMode.Client;

            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(Address), Port);
            NetEndPoint remoteEP = new NetEndPoint(ep, ep, tcpSocket, this);

            bool tcpConnected = false;
            bool udpConnected = false;

            try
            {
                await tcpSocket.ConnectAsync(Address, Port);
                tcpConnected = true;
            }
            catch (SocketException ex) { ThrowErrorOnMainThread(remoteEP, ex); }

            try
            {
                await udpSocket.ConnectAsync(Address, Port);
                udpConnected = true;
            }
            catch (SocketException ex) { ThrowErrorOnMainThread(remoteEP, ex); }

            if (tcpConnected && udpConnected)
            {
                try
                {
                    if (!connectionsTCP.TryAdd(ep, remoteEP))
                    {
                        throw new Exception("Failed to add NetEndPoint to connectionsTCP");
                    }
                    if (!connectionsUDP.TryAdd(ep, remoteEP))
                    {
                        throw new Exception("Failed to add NetEndPoint to connectionsUDP");
                    }
                    var receivedPacket = await ReceiveTCPAsync(remoteEP, new CancellationTokenSource(NetConstants.DATA_RECEIVE_TIMEOUT), true);
                    if (receivedPacket.ReadInt(false) == (int)RequestStatus.Accept)
                    {
                        bool sentUDPPort = false;
                        using (NetPacket udpDataPacket = new NetPacket(this, PacketProtocol.TCP, 0))
                        {
                            udpDataPacket.Write(((IPEndPoint)udpSocket.LocalEndPoint).Port);
                            sentUDPPort = await SendInternal(remoteEP, udpDataPacket.ByteSegment, PacketProtocol.TCP, true, false);
                        }

                        if (sentUDPPort)
                        {
                            systemConnected = true;
                            ConnectOnMainThread(remoteEP);
                            ReceivePackets();
                            beginReceiveQueue.Enqueue(remoteEP);
                        }
                        else
                        {
                            udpSocket.Close();
                        }
                    }
                    else
                    {
                        NetDisconnect disconnect;
                        if (receivedPacket.ReadInt(false) == (int)RequestStatus.Deny)
                        {
                            disconnect = new NetDisconnect(DisconnectCode.ConnectionRejected);
                        }
                        else
                        {
                            disconnect = new NetDisconnect(DisconnectCode.InvalidPacket);
                        }
                        DisconnectOnMainThread(remoteEP, disconnect, false, false);
                    }
                }
                catch (SocketException ex)
                {
                    ThrowErrorOnMainThread(remoteEP, ex);
                    DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode), false, false);
                }
                catch (OperationCanceledException)
                {
                    DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionLost), false, false);
                }
            }
        }

        private void ConnectOnMainThread(NetEndPoint remoteEP)
        {
            ThreadManager.ExecuteOnMainThread(() => OnConnected?.Invoke(remoteEP));
        }

        public void Listen()
        {
            // Checks to prevent method from being called twice
            if (systemConnected)
            {
                throw new InvalidOperationException("Please call 'Close' before calling 'Listen' again.");
            }

            InitFinalObjects();
            Mode = SystemMode.Listener;

            IPEndPoint ep = new IPEndPoint(IPAddress.Any, Port);
            tcpSocket.Bind(ep);
            udpSocket.Bind(ep);

            tcpSocket.Listen(MaxPendingConnections);
            systemConnected = true;

            AcceptClients();
            ReceivePackets();
        }

        private void AcceptClients()
        {
            Task.Run(async () =>
            {
                while (!mainCancelTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        Socket clientTcpSock = await tcpSocket.AcceptAsync();
                        NetEndPoint clientEP = new NetEndPoint((IPEndPoint)clientTcpSock.RemoteEndPoint, clientTcpSock, this);
                        NetRequest request = new NetRequest(clientEP, this);

                        HandleConnectionRequestOnMainThread(request);
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.OperationAborted)
                        {
                            ThrowErrorOnMainThread(null, ex);
                        }
                    }
                }
            }, mainCancelTokenSource.Token);
        }

        private void HandleConnectionRequestOnMainThread(NetRequest request)
        {
            ThreadManager.ExecuteOnMainThread(() => OnConnectionRequest?.Invoke(request));
        }

        internal void HandleConnectionResult(bool result, NetEndPoint remoteEP)
        {
            Task.Run(async () =>
            {
                if (!connectionsTCP.TryAdd(remoteEP.TCPEndPoint, remoteEP))
                {
                    throw new Exception("Failed to add NetEndPoint to connectionsTCP");
                }

                try
                {
                    if (result)
                    {
                        SendRequestAccept(remoteEP);
                        NetPacket udpPortData = await ReceiveTCPAsync(remoteEP, new CancellationTokenSource(NetConstants.DATA_RECEIVE_TIMEOUT), true);
                        try
                        {
                            int port = udpPortData.ReadInt();
                            // Check if the port is out of range (1-65535)
                            if (port < 1 || port > 65535)
                            {
                                DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.InvalidPacket), false, false);
                            }
                            else
                            {
                                IPEndPoint endPoint = new IPEndPoint(remoteEP.TCPEndPoint.Address, port);
                                remoteEP.UDPEndPoint = endPoint;
                                if (!connectionsUDP.TryAdd(remoteEP.UDPEndPoint, remoteEP))
                                {
                                    throw new Exception("Failed to add NetEndPoint to connectionsUDP");
                                }

                                beginReceiveQueue.Enqueue(remoteEP);
                                ConnectOnMainThread(remoteEP);
                            }
                        }
                        catch (IndexOutOfRangeException)
                        {
                            DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.InvalidPacket), false, false);
                        }
                    }
                    else
                    {
                        SendRequestDeny(remoteEP);
                    }
                }
                catch (OperationCanceledException)
                {
                    DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionLost), false, false);
                }
            }, mainCancelTokenSource.Token);
        }

        private void ReceivePackets()
        {
            Task.Run(() => StartReceivingUDP(), mainCancelTokenSource.Token);
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
            Task.Run(() => Heartbeat(NetConstants.HeartbeatInterval), mainCancelTokenSource.Token);
        }

        public void Send(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol)
        {
            if (packet.Length > (protocol == PacketProtocol.TCP ? TCP.MaxPacketSize : UDP.MaxPacketSize))
            {
                throw new Exception("Packets cannot be larger than " + (protocol == PacketProtocol.TCP ? "TCP." : "UDP.") + "MaxPacketSize.");
            }

            packet.InsertLength();
            SendOnMainThread(remoteEP, packet, protocol, true);
        }

        private void SendRequestAccept(NetEndPoint remoteEP)
        {
            using (NetPacket packet = new NetPacket(this, PacketProtocol.TCP, 0))
            {
                packet.Write((int)RequestStatus.Accept);
                SendOnMainThread(remoteEP, packet, PacketProtocol.TCP, true);
            }
        }

        private void SendRequestDeny(NetEndPoint remoteEP)
        {
            using (NetPacket packet = new NetPacket(this, PacketProtocol.TCP, 0))
            {
                packet.Write((int)RequestStatus.Deny);
                SendOnMainThread(remoteEP, packet, PacketProtocol.TCP, false);
                DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionRejected), false, false);
            }
        }

        private void SendOnMainThread(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol, bool disconnectOnError)
        {
            if (packet.Protocol != protocol)
            {
                throw new Exception("Packet protocols do not match.");
            }

            packet.StartIndex = 0;

            // We must create a new buffer because the passed packet will most likely be disposed by the time SendInternal gets invoked
            byte[] buffer = packetPool.Rent((protocol == PacketProtocol.TCP ? TCP.MaxPacketSize : UDP.MaxPacketSize) + sizeof(int));
            Buffer.BlockCopy(packet.ByteArray, packet.StartIndex, buffer, 0, packet.Length);
            ArraySegment<byte> packetSegment = new ArraySegment<byte>(buffer, 0, packet.Length);
            ThreadManager.ExecuteOnMainThread(async () => await SendInternal(remoteEP, packetSegment, protocol, disconnectOnError, true));
        }

        private async Task<bool> SendInternal(NetEndPoint remoteEP, ArraySegment<byte> packetSegment, PacketProtocol protocol, bool disconnectOnError, bool disposeBuffer)
        {
            try
            {
                if (!connectionsTCP.ContainsKey((IPEndPoint)remoteEP.tcpSocket.RemoteEndPoint))
                {
                    return false;
                }
            }
            catch (ObjectDisposedException) { return false; }

            bool returnValue = false;

            try
            {
                if (protocol == PacketProtocol.TCP)
                {
                    await remoteEP.tcpSocket.SendAsync(packetSegment, SocketFlags.None);
                }
                else
                {
                    if (remoteEP.UDPEndPoint != null)
                    {
                        await udpSocket.SendToAsync(packetSegment, SocketFlags.None, remoteEP.UDPEndPoint);
                    }
                }

                returnValue = true;
            }
            catch (SocketException ex)
            {
                OnNetworkError?.Invoke(remoteEP, ex);
                if (disconnectOnError)
                {
                    await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode), false, false);
                }
            }

            if (disposeBuffer)
            {
                packetPool.Return(packetSegment.Array);
            }

            return returnValue;
        }

        public void Disconnect(NetEndPoint remoteEP)
        {
            DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosed), true, false);
        }

        public void Disconnect(NetEndPoint remoteEP, NetPacket disconnectPacket)
        {
            if (disconnectPacket.Protocol != PacketProtocol.TCP)
            {
                throw new Exception("Packet protocol for disconnect packets must be PacketProtocol.TCP.");
            }

            disconnectPacket.InsertLength();
            disconnectPacket.StartIndex = 0;
            // We must create a new packet because the passed packet will most likely be disposed by the time DisconnectInternal gets invoked
            byte[] buffer = packetPool.Rent(TCP.MaxPacketSize + sizeof(int));
            Buffer.BlockCopy(disconnectPacket.ByteArray, disconnectPacket.StartIndex, buffer, 0, disconnectPacket.Length);
            NetPacket packet = new NetPacket(buffer, PacketProtocol.TCP);
            // Manually set the start index and length because the constructor used above sets them to 0 (StartIndex is 0 because the length integer needs to be copied over in DisconnectInternal)
            packet.StartIndex = 0;
            packet.Length = disconnectPacket.Length;
            DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosedWithMessage, packet), true, true);
        }

        public async void DisconnectForcefully(NetEndPoint remoteEP)
        {
            await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosedForcefully), false, false);
        }

        private void DisconnectOnMainThread(NetEndPoint remoteEP, NetDisconnect disconnect, bool sendDisconnectPacketToRemote, bool disposeDisconnectPacket)
        {
            ThreadManager.ExecuteOnMainThread(async () => await DisconnectInternal(remoteEP, disconnect, sendDisconnectPacketToRemote, disposeDisconnectPacket));
        }

        private async Task<bool> DisconnectInternal(NetEndPoint remoteEP, NetDisconnect disconnect, bool sendDisconnectPacketToRemote, bool disposeDisconnectPacket)
        {
            bool returnValue = true;

            try
            {
                if (!connectionsTCP.ContainsKey((IPEndPoint)remoteEP.tcpSocket.RemoteEndPoint))
                {
                    return false;
                }
            }
            catch (ObjectDisposedException) { return false; }

            if (sendDisconnectPacketToRemote)
            {
                using (NetPacket packet = new NetPacket(this, PacketProtocol.TCP, 0))
                {
                    packet.Write((int)disconnect.DisconnectCode);
                    if (disconnect.DisconnectData != null)
                    {
                        // The length integer is already written in the disconnect packet (from the Disconnect method)
                        packet.WriteInternal(disconnect.DisconnectData.ByteSegment.ToArray());
                        // Set the start index back to 4 for when OnDisconnected is invoked
                        disconnect.DisconnectData.StartIndex = 4;
                        // Set the current index back to 0 for when OnDisconnected is invoked
                        disconnect.DisconnectData.CurrentIndex = 0;
                    }

                    returnValue = await SendInternal(remoteEP, packet.ByteSegment, PacketProtocol.TCP, false, false);
                }
            }

            CloseRemote(remoteEP);
            OnDisconnected?.Invoke(remoteEP, disconnect);

            if (disposeDisconnectPacket && disconnect.DisconnectData != null)
            {
                disconnect.DisconnectData.Dispose();
            }

            return returnValue;
        }

        public void Close(bool sendDisconnectPacketToRemote)
        {
            foreach (var remoteEP in connectionsTCP.Values)
            {
                DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosed), sendDisconnectPacketToRemote, false);
            }

            ThreadManager.ExecuteOnMainThread(() => Reset(true));
        }

        private void CloseRemote(NetEndPoint remoteEP)
        {
            remoteEP.tcpCancelTokenSource.Cancel();
            remoteEP.tcpSocket.Shutdown(SocketShutdown.Both);
            remoteEP.tcpSocket.Close();
            if (!connectionsTCP.TryRemove(remoteEP.TCPEndPoint, out _))
            {
                throw new Exception("Failed to remove NetEndPoint from connectionsTCP.");
            }
            if (remoteEP.UDPEndPoint != null)
            {
                if (!connectionsUDP.TryRemove(remoteEP.UDPEndPoint, out _))
                {
                    throw new Exception("Failed to remove NetEndPoint from connectionsUDP.");
                }
            }
        }

        private void ReceivePacketOnMainThread(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol)
        {
            ThreadManager.ExecuteOnMainThread(() =>
            {
                OnPacketReceive?.Invoke(remoteEP, packet, protocol);
                packet.Dispose();
            });
        }

        private void ThrowErrorOnMainThread(NetEndPoint remoteEP, SocketException ex)
        {
            ThreadManager.ExecuteOnMainThread(() => OnNetworkError?.Invoke(remoteEP, ex));
        }

        public void Update()
        {
            ThreadManager.PollMainThread();
        }

        private async void StartReceivingTCP(NetEndPoint netEndPoint)
        {
            bool disconnect = false;
            while (!mainCancelTokenSource.IsCancellationRequested && !disconnect)
            {
                byte[] buffer = packetPool.Rent(TCP.BufferSize);
                try
                {
                    NetPacket receivedPacket = new NetPacket(buffer, PacketProtocol.TCP);
                    NetPacket finalPacket;
                    bool validPacket;

                    (finalPacket, validPacket) = await ReceiveTCPAsync(netEndPoint, receivedPacket, buffer, netEndPoint.tcpCancelTokenSource);

                    if (!validPacket)
                    {
                        disconnect = await ProcessStatusPacket(finalPacket, netEndPoint, receivedPacket);
                    }

                    if (validPacket)
                    {
                        ReceivePacketOnMainThread(netEndPoint, finalPacket, PacketProtocol.TCP);
                    }
                }
                catch (SocketException ex)
                {
                    ThrowErrorOnMainThread(netEndPoint, ex);
                    DisconnectOnMainThread(netEndPoint, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode), false, false);
                    disconnect = true;
                }
                catch (OperationCanceledException)
                {
                    disconnect = true;
                }
                packetPool.Return(buffer);
            }
        }

        private async Task<(NetPacket, bool)> ReceiveTCPAsync(NetEndPoint netEndPoint, NetPacket receivedPacket, byte[] buffer, CancellationTokenSource tcpCancelSource, bool ignoreExpectedLength = false)
        {
            NetPacket finalPacket = new NetPacket(this, PacketProtocol.TCP);
            ArraySegment<byte> segBuffer = new ArraySegment<byte>(buffer, receivedPacket.Length, buffer.Length - receivedPacket.Length);
            int expectedLength = 0;
            bool grabbedPacketLength = false;
            bool validPacket = false;

            while (!mainCancelTokenSource.IsCancellationRequested)
            {
                // If there are enough bytes to form an int
                if (receivedPacket.Length >= 4)
                {
                    // If we haven't already grabbed the packet length
                    if (!grabbedPacketLength && !ignoreExpectedLength)
                    {
                        expectedLength = receivedPacket.ReadInt();

                        // Connection status packets have an expected length of less than 0, so just return the final packet so it can be acted upon later
                        if (expectedLength < 0)
                        {
                            finalPacket.Write(expectedLength);
                            break;
                        }
                        // If the expected length of the packet is greater than the set TCP buffer size
                        if (expectedLength > TCP.BufferSize)
                        {
                            finalPacket.Write((int)DisconnectCode.PacketOverBufferSize);
                            break;
                        }
                        // If the expected length of the packet is greater than the set max packet size
                        if (expectedLength > TCP.MaxPacketSize)
                        {
                            finalPacket.Write((int)DisconnectCode.PacketOverMaxSize);
                            break;
                        }

                        // Reset the timeout time for the remote end point (since we received a packet)
                        netEndPoint.tcpConnectionTimeoutTime = 0;
                        // If the expected length of the packet is 0, this is a heartbeat packet
                        if (expectedLength == 0)
                        {
                            break;
                        }
                        else
                        {
                            // If we receive a non-heartbeat packet, reset the heartbeat interval since sending a heartbeat packet is unnecessary
                            netEndPoint.tcpHearbeatInterval = 0;
                        }

                        grabbedPacketLength = true;
                    }

                    // If all the bytes in the packet have been received
                    if (expectedLength <= receivedPacket.UnreadLength || ignoreExpectedLength)
                    {
                        finalPacket.WriteInternal(receivedPacket.ReadBytesInternal(ignoreExpectedLength ? 4 : expectedLength));
                        validPacket = true;
                        break;
                    }
                }

                // If there are no more bytes already in receivedPacket, receive more
                var receivedBytes = await netEndPoint.tcpSocket.ReceiveAsync(segBuffer, SocketFlags.None, tcpCancelSource.Token);
                receivedPacket.Length += receivedBytes;

                // If a completely blank packet was sent
                if (receivedBytes == 0)
                {
                    finalPacket.Write((int)DisconnectCode.ConnectionClosedForcefully);
                    break;
                }

                segBuffer = segBuffer.Slice(segBuffer.Offset + receivedBytes);
            }

            return (finalPacket, validPacket);
        }

        private async Task<NetPacket> ReceiveTCPAsync(NetEndPoint remoteEP, CancellationTokenSource tcpCancelSource, bool ignoreExpectedLength = false)
        {
            byte[] buffer = packetPool.Rent(TCP.MaxPacketSize);
            var result = await ReceiveTCPAsync(remoteEP, new NetPacket(buffer, PacketProtocol.TCP), buffer, tcpCancelSource, ignoreExpectedLength);
            packetPool.Return(buffer);
            return result.Item1;
        }

        private async Task<(NetPacket, bool)> ReceiveTCPAsync(NetEndPoint remoteEP, NetPacket receivedPacket, CancellationTokenSource tcpCancelSource, bool ignoreExpectedLength = false)
        {
            return await ReceiveTCPAsync(remoteEP, receivedPacket, receivedPacket.ByteArray, tcpCancelSource, ignoreExpectedLength);
        }

        private async void StartReceivingUDP()
        {
            while (!mainCancelTokenSource.IsCancellationRequested)
            {
                byte[] buffer = packetPool.Rent(UDP.BufferSize);
                try
                {
                    bool validPacket;
                    NetPacket receivedPacket = new NetPacket(buffer, PacketProtocol.UDP);
                    NetPacket finalPacket;
                    NetEndPoint netEndPoint;

                    (finalPacket, netEndPoint, validPacket) = await ReceiveUDPAsync(receivedPacket, buffer);

                    if (!validPacket)
                    {
                        _ = await ProcessStatusPacket(finalPacket, netEndPoint);
                    }

                    if (validPacket)
                    {
                        ReceivePacketOnMainThread(netEndPoint, finalPacket, PacketProtocol.UDP);
                    }
                }
                catch (SocketException ex)
                {
                    if(ex.SocketErrorCode != SocketError.OperationAborted) 
                    {
                        ThrowErrorOnMainThread(null, ex);
                    }
                }
                packetPool.Return(buffer);
            }
        }

        private async Task<(NetPacket, NetEndPoint, bool)> ReceiveUDPAsync(NetPacket receivedPacket, byte[] buffer)
        {
            NetPacket finalPacket = new NetPacket(this, PacketProtocol.UDP);
            NetEndPoint remoteEP = new NetEndPoint(this);

            ArraySegment<byte> segBuffer = new ArraySegment<byte>(buffer);
            bool validPacket = false;

            while (!mainCancelTokenSource.IsCancellationRequested)
            {
                var receivedBytes = await udpSocket.ReceiveFromAsync(segBuffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));

                // Disregard packet as it is too small
                if (receivedBytes.ReceivedBytes < 4)
                {
                    continue;
                }

                // If the received data is from a currently connected end point
                if (connectionsUDP.TryGetValue((IPEndPoint)receivedBytes.RemoteEndPoint, out remoteEP))
                {
                    int expectedLength = receivedPacket.ReadInt();

                    // Packets expected length was larger than the actual amount of bytes received
                    if (expectedLength > receivedBytes.ReceivedBytes - 4)
                    {
                        finalPacket.Write((int)DisconnectCode.InvalidPacket);
                        break;
                    }
                    // Packets expected length was smaller than the actual amount of bytes received
                    if (expectedLength < receivedBytes.ReceivedBytes - 4)
                    {
                        finalPacket.Write((int)DisconnectCode.InvalidPacket);
                        break;
                    }
                    // If the expected length of the packet is greater than the set UDP buffer size
                    if (expectedLength > UDP.BufferSize)
                    {
                        finalPacket.Write((int)DisconnectCode.PacketOverBufferSize);
                        break;
                    }
                    // If the expected length of the packet is greater than the set max packet size
                    if (expectedLength > UDP.MaxPacketSize)
                    {
                        finalPacket.Write((int)DisconnectCode.PacketOverMaxSize);
                        break;
                    }

                    // Reset the timeout time for the remote end point (since we received a packet)
                    remoteEP.udpConnectionTimeoutTime = 0;
                    // If the expected length of the packet is 0, this is a heartbeat packet
                    if (expectedLength == 0)
                    {
                        break;
                    }
                    else
                    {
                        // If we receive a non-heartbeat packet, reset the heartbeat interval since sending a heartbeat packet is unnecessary
                        remoteEP.udpHearbeatInterval = 0;
                    }

                    finalPacket.WriteInternal(receivedPacket.ReadBytesInternal(expectedLength));
                    validPacket = true;
                    break;
                }
            }

            return (finalPacket, remoteEP, validPacket);
        }

        private async void Heartbeat(int heartbeatInterval)
        {
            try
            {
                while (!mainCancelTokenSource.IsCancellationRequested)
                {
                    foreach (var remoteEP in connectionsTCP.Values)
                    {
                        if (remoteEP.tcpConnectionTimeoutTime >= NetConstants.TCP_CONNECTION_TIMEOUT || remoteEP.udpConnectionTimeoutTime >= NetConstants.UDP_CONNECTION_TIMEOUT)
                        {
                            DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionLost), false, false);
                            break;
                        }

                        if (remoteEP.tcpHearbeatInterval >= NetConstants.TCP_HEARTBEAT_INTERVAL)
                        {
                            using (NetPacket packet = new NetPacket(this, PacketProtocol.TCP, 0))
                            {
                                packet.Write(0);
                                SendOnMainThread(remoteEP, packet, PacketProtocol.TCP, false);
                            }
                            remoteEP.tcpHearbeatInterval = 0;
                        }

                        if (remoteEP.udpHearbeatInterval >= NetConstants.UDP_HEARTBEAT_INTERVAL)
                        {
                            using (NetPacket packet = new NetPacket(this, PacketProtocol.UDP, 0))
                            {
                                packet.Write(0);
                                SendOnMainThread(remoteEP, packet, PacketProtocol.UDP, false);
                            }
                            remoteEP.udpHearbeatInterval = 0;
                        }

                        remoteEP.tcpConnectionTimeoutTime += heartbeatInterval / 1000f;
                        remoteEP.tcpHearbeatInterval += heartbeatInterval / 1000f;
                        remoteEP.udpConnectionTimeoutTime += heartbeatInterval / 1000f;
                        remoteEP.udpHearbeatInterval += heartbeatInterval / 1000f;
                    }
                    await Task.Delay(heartbeatInterval, mainCancelTokenSource.Token);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task<bool> ProcessStatusPacket(NetPacket finalPacket, NetEndPoint netEndPoint, NetPacket receivedPacket = null)
        {
            // Heartbeat packet, don't disconnect
            if (finalPacket.UnreadLength == 0)
            {
                return false;
            }

            int code = finalPacket.ReadInt();
            bool containsDisconnectCode = false;

            foreach (int c in Enum.GetValues(typeof(DisconnectCode)))
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
                    if (code == (int)DisconnectCode.ConnectionClosedWithMessage && receivedPacket != null)
                    {
                        NetPacket finalData;
                        bool validPacket;
                        (finalData, validPacket) = await ReceiveTCPAsync(netEndPoint, receivedPacket, new CancellationTokenSource(NetConstants.DATA_RECEIVE_TIMEOUT));

                        if (validPacket)
                        {
                            disconnect = new NetDisconnect((DisconnectCode)code, finalData);
                        }
                        else
                        {
                            disconnect = new NetDisconnect(DisconnectCode.InvalidPacket);
                        }
                    }
                    else
                    {
                        disconnect = new NetDisconnect((DisconnectCode)code);
                    }
                }
                else
                {
                    disconnect = new NetDisconnect(DisconnectCode.InvalidPacket);
                }
            }
            catch (OperationCanceledException)
            {
                disconnect = new NetDisconnect(DisconnectCode.InvalidPacket);
            }

            DisconnectOnMainThread(netEndPoint, disconnect, false, true);
            return true;
        }

        private void Reset(bool clearUnmanaged)
        {
            mainCancelTokenSource.Cancel();

            if (clearUnmanaged)
            {
                tcpSocket.Close();
                udpSocket.Close();
                mainCancelTokenSource.Dispose();
            }

            connectionsTCP.Clear();
            connectionsUDP.Clear();
            beginReceiveQueue.Clear();
            systemConnected = false;
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
                    Reset(false);
                    connectionsTCP = null;
                    connectionsUDP = null;
                    beginReceiveQueue = null;
                    packetPool = null;
                }

                tcpSocket.Dispose();
                udpSocket.Dispose();
                mainCancelTokenSource.Dispose();

                disposed = true;
            }
        }

        ~NetSystem()
        {
            Dispose(false);
        }
    }
}
