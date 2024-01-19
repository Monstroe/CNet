using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MonstroeNet
{
    public class Protocol
    {
        public int BufferSize { get; set; }
        public int MaxPacketSize { get; set; }
    }

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
        public delegate void NetworkErrorHandler(SocketException socketException);

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

        public Protocol TCP { get; }
        public Protocol UDP { get; }

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

        private Dictionary<IPEndPoint, NetEndPoint> connectionsTCP;
        private Dictionary<IPEndPoint, NetEndPoint> connectionsUDP;
        private Socket tcpSocket;
        private Socket udpSocket;
        private bool systemConnected;
        private ConcurrentQueue<NetEndPoint> beginReceiveQueue;
        private CancellationTokenSource cancellationTokenSource;
        private ArrayPool<byte> packetPool;

        public NetSystem()
        {
            MaxPendingConnections = 100;

            TCP = new Protocol();
            UDP = new Protocol();

            TCP.BufferSize = 1024;
            TCP.MaxPacketSize = 50;

            UDP.BufferSize = 4096;
            UDP.MaxPacketSize = 50;

            connectionsTCP = new Dictionary<IPEndPoint, NetEndPoint>();
            connectionsUDP = new Dictionary<IPEndPoint, NetEndPoint>();
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

            cancellationTokenSource = new CancellationTokenSource();

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
            NetEndPoint remoteEP = new NetEndPoint(ep, tcpSocket, this);
            remoteEP.UDPEndPoint = ep;

            bool tcpConnected = false;
            bool udpConnected = false;

            try
            {
                await tcpSocket.ConnectAsync(Address, Port);
                tcpConnected = true;
            }
            catch (SocketException ex) { ThrowErrorOnMainThread(ex); }

            try
            {
                await udpSocket.ConnectAsync(Address, Port);
                udpConnected = true;
            }
            catch (SocketException ex) { ThrowErrorOnMainThread(ex); }

            if (tcpConnected && udpConnected)
            {
                try
                {
                    connectionsTCP.Add(ep, remoteEP);
                    connectionsUDP.Add(ep, remoteEP);
                    var receivedPacket = await ReceiveTCPAsync(remoteEP, true);
                    if (receivedPacket.ReadInt(false) == (int)RequestStatus.Accept)
                    {
                        bool sentUDPPort = false;
                        using (NetPacket udpDataPacket = new NetPacket())
                        {
                            udpDataPacket.Write(((IPEndPoint)udpSocket.LocalEndPoint).Port);
                            sentUDPPort = await SendInternal(remoteEP, udpDataPacket, PacketProtocol.TCP);
                        }

                        if (sentUDPPort)
                        {
                            systemConnected = true;
                            ReceivePackets();
                            beginReceiveQueue.Enqueue(remoteEP);
                            OnConnected?.Invoke(remoteEP);
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
                            disconnect = new NetDisconnect(DisconnectCode.ConnectionRejected);
                        else
                        {
                            disconnect = new NetDisconnect(DisconnectCode.InvalidPacket);
                        }
                        DisconnectOnMainThread(remoteEP, disconnect, false);
                    }
                }
                catch (SocketException ex)
                {
                    ThrowErrorOnMainThread(ex);
                    DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode), false);
                }
            }
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
                while (!cancellationTokenSource.IsCancellationRequested)
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
                            ThrowErrorOnMainThread(ex);
                        }
                    }
                    //catch (ObjectDisposedException) { /* Catch this error when 'Close' is called */ }
                }
            }, cancellationTokenSource.Token);
        }

        private void HandleConnectionRequestOnMainThread(NetRequest request)
        {
            ThreadManager.ExecuteOnMainThread(() => OnConnectionRequest?.Invoke(request));
        }

        internal void HandleConnectionResultOnMainThread(bool result, NetEndPoint remoteEP)
        {
            ThreadManager.ExecuteOnMainThread(async () =>
            {
                connectionsTCP.Add(remoteEP.TCPEndPoint, remoteEP);
                if (result)
                {
                    await SendRequestAccept(remoteEP);
                    NetPacket udpPortData = await ReceiveTCPAsync(remoteEP, true);
                    try
                    {
                        int port = udpPortData.ReadInt();
                        IPEndPoint endPoint = new IPEndPoint(remoteEP.TCPEndPoint.Address, port);
                        remoteEP.UDPEndPoint = endPoint;
                        connectionsUDP.Add(remoteEP.UDPEndPoint, remoteEP);
                    }
                    catch (IndexOutOfRangeException ex)
                    {
                        await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectCode.InvalidPacket), false);
                    }

                    beginReceiveQueue.Enqueue(remoteEP);
                    OnConnected?.Invoke(remoteEP);
                }
                else
                {
                    await SendRequestDeny(remoteEP);
                }
            });
        }

        private void ReceivePackets()
        {
            Task.Run(() => StartReceivingUDP(), cancellationTokenSource.Token);
            Task.Run(() =>
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    if (beginReceiveQueue.TryDequeue(out var netEndPoint))
                    {
                        StartReceivingTCP(netEndPoint);
                    }
                }
            }, cancellationTokenSource.Token);
        }

        public void Send(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol)
        {
            if (packet.Length > (protocol == PacketProtocol.TCP ? TCP.MaxPacketSize : UDP.MaxPacketSize))
            {
                throw new Exception("Packets cannot be larger than " + (protocol == PacketProtocol.TCP ? "TCP." : "UDP.") + "MaxPacketSize.");
            }

            packet.InsertLength();

            byte[] packetBytes = packet.ByteArray;
            ThreadManager.ExecuteOnMainThread(async () => await SendInternal(remoteEP, packetBytes, protocol));
        }

        private async Task<bool> SendInternal(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol, bool disconnectOnError = true)
        {
            byte[] packetBytes = packet.ByteArray;
            return await SendInternal(remoteEP, packetBytes, protocol, disconnectOnError);
        }

        private async Task<bool> SendInternal(NetEndPoint remoteEP, byte[] packetBytes, PacketProtocol protocol, bool disconnectOnError = true)
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
            ArraySegment<byte> segBuffer = new ArraySegment<byte>(packetBytes);

            try
            {
                if (protocol == PacketProtocol.TCP)
                {
                    await remoteEP.tcpSocket.SendAsync(segBuffer, SocketFlags.None);
                }
                else
                {
                    if (remoteEP.UDPEndPoint != null)
                    {
                        await udpSocket.SendToAsync(segBuffer, SocketFlags.None, remoteEP.UDPEndPoint);
                    }
                }

                returnValue = true;
            }
            catch (SocketException ex)
            {
                OnNetworkError?.Invoke(ex);
                if (disconnectOnError)
                {
                    await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode), false);
                }
            }

            return returnValue;
        }

        public void Disconnect(NetEndPoint remoteEP)
        {
            DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosed), true);
        }

        public void Disconnect(NetEndPoint remoteEP, NetPacket disconnectPacket)
        {
            DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosedWithMessage, disconnectPacket.ByteArray), true);
        }

        public async void DisconnectForcefully(NetEndPoint remoteEP)
        {
            await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosedForcefully), false);
        }

        private void DisconnectOnMainThread(NetEndPoint remoteEP, NetDisconnect disconnect, bool sendDisconnectPacketToRemote)
        {
            ThreadManager.ExecuteOnMainThread(async () => await DisconnectInternal(remoteEP, disconnect, sendDisconnectPacketToRemote));
        }

        private async Task<bool> DisconnectInternal(NetEndPoint remoteEP, NetDisconnect disconnect, bool sendDisconnectPacketToRemote)
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
                using (NetPacket packet = new NetPacket())
                {
                    packet.Write((int)disconnect.DisconnectCode);
                    if (disconnect.DisconnectData != null)
                    {
                        packet.Write(disconnect.DisconnectData.ByteArray);
                        packet.InsertLength(4);
                    }

                    returnValue = await SendInternal(remoteEP, packet, PacketProtocol.TCP, false);
                }
            }

            CloseRemote(remoteEP);
            OnDisconnected?.Invoke(remoteEP, disconnect);

            return returnValue;
        }

        public void Close(bool sendDisconnectPacketToRemote)
        {
            foreach (var remoteEP in connectionsTCP.Values)
            {
                DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosed), sendDisconnectPacketToRemote);
            }

            ThreadManager.ExecuteOnMainThread(() => Reset(true));
        }

        private void CloseRemote(NetEndPoint remoteEP)
        {
            remoteEP.cancellationTokenSource.Cancel();
            remoteEP.tcpSocket.Shutdown(SocketShutdown.Both);
            remoteEP.tcpSocket.Close();
            connectionsTCP.Remove(remoteEP.TCPEndPoint);
            connectionsUDP.Remove(remoteEP.UDPEndPoint);
        }

        private async Task SendRequestAccept(NetEndPoint remoteEP)
        {
            using (NetPacket packet = new NetPacket())
            {
                packet.Write((int)RequestStatus.Accept);
                await SendInternal(remoteEP, packet, PacketProtocol.TCP);
            }
        }

        private async Task SendRequestDeny(NetEndPoint remoteEP)
        {
            using (NetPacket packet = new NetPacket())
            {
                packet.Write((int)RequestStatus.Deny);
                await SendInternal(remoteEP, packet, PacketProtocol.TCP, false);
                await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectCode.ConnectionRejected), false);
            }
        }

        private void ReceivePacketOnMainThread(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol)
        {
            ThreadManager.ExecuteOnMainThread(() => OnPacketReceive?.Invoke(remoteEP, packet, protocol));
        }

        private void ThrowErrorOnMainThread(SocketException ex)
        {
            ThreadManager.ExecuteOnMainThread(() => OnNetworkError?.Invoke(ex));
        }

        public void Update()
        {
            ThreadManager.PollMainThread();
        }

        private async void StartReceivingTCP(NetEndPoint netEndPoint)
        {
            byte[] buffer = packetPool.Rent(TCP.BufferSize);
            NetPacket receivedPacket = new NetPacket();
            bool validPacket;

            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    NetPacket finalPacket;
                    (finalPacket, validPacket) = await ReceiveTCPAsync(netEndPoint, receivedPacket, buffer);

                    if (!validPacket)
                    {
                        bool breakLoop = await ProcessStatusPacket(finalPacket, netEndPoint, receivedPacket);

                        if (breakLoop)
                            break;
                    }
                    receivedPacket.Remove(0, receivedPacket.CurrentIndex);

                    if (validPacket)
                    {
                        ReceivePacketOnMainThread(netEndPoint, finalPacket, PacketProtocol.TCP);
                    }
                }
            }
            catch (SocketException ex)
            {
                ThrowErrorOnMainThread(ex);
                DisconnectOnMainThread(netEndPoint, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode), false);
            }
            catch (OperationCanceledException) { /* Catch this error when the tcp cancellation token was canceled */ }

            receivedPacket.Dispose();
            packetPool.Return(buffer);
        }

        private async Task<(NetPacket, bool)> ReceiveTCPAsync(NetEndPoint netEndPoint, NetPacket receivedPacket, byte[] buffer, bool ignoreExpectedLength = false)//, bool inRecursiveCall = false)
        {
            NetPacket finalPacket = new NetPacket();
            ArraySegment<byte> segBuffer = new ArraySegment<byte>(buffer);
            int expectedLength = 0;
            bool grabbedPacketLength = false;
            bool validPacket = false;

            while (!cancellationTokenSource.IsCancellationRequested)
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
                        else if (expectedLength > TCP.BufferSize)
                        {
                            finalPacket.Write((int)DisconnectCode.PacketOverBufferSize);
                            break;
                        }
                        // If the expected length of the packet is greater than the set max packet size
                        else if (expectedLength > TCP.MaxPacketSize)
                        {
                            finalPacket.Write((int)DisconnectCode.PacketOverMaxSize);
                            break;
                        }

                        grabbedPacketLength = true;
                    }

                    // If all the bytes in the packet have been received
                    if (expectedLength <= receivedPacket.UnreadLength || ignoreExpectedLength)
                    {
                        finalPacket.Write(receivedPacket.ReadBytes(ignoreExpectedLength ? 4 : expectedLength));
                        validPacket = true;
                        break;
                    }
                }

                // If there are no more bytes already in receivedPacket, receive more
                var receivedBytes = await netEndPoint.tcpSocket.ReceiveAsync(segBuffer, SocketFlags.None, netEndPoint.cancellationTokenSource.Token);

                // If a completely blank packet was sent
                if (receivedBytes == 0)
                {
                    finalPacket.Write((int)DisconnectCode.ConnectionClosedForcefully);
                    break;
                }

                receivedPacket.Write(segBuffer.Slice(segBuffer.Offset, receivedBytes).ToArray());
                segBuffer = segBuffer.Slice(segBuffer.Offset + receivedBytes);
            }

            Buffer.BlockCopy(buffer, receivedPacket.CurrentIndex, buffer, 0, receivedPacket.UnreadLength);   // <<-- TODO
            return (finalPacket, validPacket);
        }

        private async Task<NetPacket> ReceiveTCPAsync(NetEndPoint remoteEP, bool ignoreExpectedLength = false)
        {
            return (await ReceiveTCPAsync(remoteEP, new NetPacket(), new byte[TCP.MaxPacketSize], ignoreExpectedLength)).Item1;
        }

        private async Task<NetPacket> ReceiveTCPAsync(NetEndPoint remoteEP, NetPacket receivedPacket, bool ignoreExpectedLength = false)
        {
            return (await ReceiveTCPAsync(remoteEP, receivedPacket, new byte[TCP.MaxPacketSize], ignoreExpectedLength)).Item1;
        }

        private async void StartReceivingUDP()
        {
            NetPacket receivedPacket = new NetPacket();
            bool validPacket;

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    NetPacket finalPacket;
                    NetEndPoint netEndPoint;

                    (finalPacket, netEndPoint, validPacket) = await ReceiveUDPAsync(receivedPacket);

                    if (!validPacket)
                    {
                        await ProcessStatusPacket(finalPacket, netEndPoint);
                        break;
                    }

                    receivedPacket.Clear();
                    ReceivePacketOnMainThread(netEndPoint, finalPacket, PacketProtocol.UDP);
                }
                catch (SocketException ex)
                {
                    if(ex.SocketErrorCode != SocketError.OperationAborted) 
                    {
                        ThrowErrorOnMainThread(ex);
                    }
                }
                //catch (OperationCanceledException) { /* Catch this error when the cancellation token was canceled */ }
                //catch (ObjectDisposedException) { /* Catch this error when 'Close' is called */ }
            }

            receivedPacket.Dispose();
        }

        private async Task<(NetPacket, NetEndPoint, bool)> ReceiveUDPAsync(NetPacket receivedPacket)
        {
            NetPacket finalPacket = new NetPacket();
            NetEndPoint remoteEP = new NetEndPoint(this);
            byte[] buffer = packetPool.Rent(UDP.BufferSize);
            ArraySegment<byte> segBuffer = new ArraySegment<byte>(buffer);
            bool validPacket = false;

            while (!cancellationTokenSource.IsCancellationRequested)
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
                    receivedPacket.Write(segBuffer.Slice(0, 4).ToArray());
                    int expectedLength = receivedPacket.ReadInt();

                    // Partial packet received, disregard
                    if (expectedLength > receivedBytes.ReceivedBytes)
                    {
                        continue;
                    }

                    // Packets expected length was smaller than the actual amount of bytes received
                    if (expectedLength < receivedBytes.ReceivedBytes - 4)
                    {
                        finalPacket.Write((int)DisconnectCode.InvalidPacket);
                        break;
                    }
                    // If the expected length of the packet is greater than the set UDP buffer size
                    else if (expectedLength > UDP.BufferSize)
                    {
                        finalPacket.Write((int)DisconnectCode.PacketOverBufferSize);
                        break;
                    }
                    // If the expected length of the packet is greater than the set max packet size
                    else if (expectedLength > UDP.MaxPacketSize)
                    {
                        finalPacket.Write((int)DisconnectCode.PacketOverMaxSize);
                        break;
                    }

                    finalPacket.Write(segBuffer.Slice(4, receivedBytes.ReceivedBytes - 4).ToArray());
                    validPacket = true;
                    break;
                }
            }

            packetPool.Return(buffer);
            return (finalPacket, remoteEP, validPacket);
        }

        private async Task<bool> ProcessStatusPacket(NetPacket finalPacket, NetEndPoint netEndPoint, NetPacket receivedPacket = null)
        {
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
            if (containsDisconnectCode)
            {
                if (code == (int)DisconnectCode.ConnectionClosedWithMessage && receivedPacket != null)
                {
                    NetPacket finalData = await ReceiveTCPAsync(netEndPoint, receivedPacket);
                    disconnect = new NetDisconnect((DisconnectCode)code, finalData);
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

            DisconnectOnMainThread(netEndPoint, disconnect, false);
            return true;
        }

        private void Reset(bool clearUnmanaged)
        {
            cancellationTokenSource.Cancel();

            if (clearUnmanaged)
            {
                tcpSocket.Close();
                udpSocket.Close();
                cancellationTokenSource.Dispose();
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
                    systemConnected = false;
                }

                tcpSocket.Dispose();
                udpSocket.Dispose();
                cancellationTokenSource.Dispose();

                disposed = true;
            }
        }

        ~NetSystem()
        {
            Dispose(false);
        }
    }
}
