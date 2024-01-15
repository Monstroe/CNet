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
                if (connections.Count == 1)
                    return connections.Values.ToList().First();
                else
                    return null;
            }
        }

        public List<NetEndPoint> RemoteEndPoints
        {
            get { return connections.Values.ToList(); }
        }

        public List<NetEndPoint> RemoteUDPEndPoints
        {
            get { return connectionsUDP.Values.ToList(); }
        }

        private const int UDPPortReceiveCode = -100;

        private Dictionary<IPEndPoint, NetEndPoint> connections;
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

            connections = new Dictionary<IPEndPoint, NetEndPoint>();
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
            remoteEP.udpEndPoint = ep;

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
                    connections.Add(ep, remoteEP);
                    connectionsUDP.Add(ep, remoteEP);
                    var receivedPacket = await ReceiveTCPAsync(remoteEP, true);
                    if (receivedPacket.ReadInt(false) == (int)RequestStatus.Accept)
                    {
                        bool sentUDPPort = false;
                        using (NetPacket udpDataPacket = new NetPacket())
                        {
                            udpDataPacket.Write(UDPPortReceiveCode);
                            udpDataPacket.Write(((IPEndPoint)udpSocket.LocalEndPoint).Port);
                            udpDataPacket.InsertLength(4);
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
                            disconnect = new NetDisconnect(DisconnectCode.InvalidPacket);
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
                connections.Add(remoteEP.EndPoint, remoteEP);
                if (result)
                {
                    beginReceiveQueue.Enqueue(remoteEP);
                    await SendRequestAccept(remoteEP);
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

            ThreadManager.ExecuteOnMainThread(async () => await SendInternal(remoteEP, packet, protocol));
        }

        private async Task<bool> SendInternal(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol, bool disconnectOnError = true)
        {
            bool returnValue = false;
            ArraySegment<byte> segBuffer = new ArraySegment<byte>(packet.ByteArray);

            try
            {
                if (protocol == PacketProtocol.TCP)
                {
                    await remoteEP.tcpSocket.SendAsync(segBuffer, SocketFlags.None);
                }
                else
                {
                    await udpSocket.SendToAsync(segBuffer, SocketFlags.None, remoteEP.EndPoint);
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
            catch (ObjectDisposedException) { /* Catch this error when 'CloseRemote' is called */ }

            return returnValue;
        }

        public void Disconnect(NetEndPoint remoteEP)
        {
            DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosed), true);
        }

        public void Disconnect(NetEndPoint remoteEP, NetPacket disconnectPacket)
        {
            DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosedWithMessage, disconnectPacket), true);
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
            foreach (var remoteEP in connections.Values)
            {
                DisconnectOnMainThread(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosed), sendDisconnectPacketToRemote);
            }

            ThreadManager.ExecuteOnMainThread(() => Reset(true));
        }

        private void CloseRemote(NetEndPoint remoteEP)
        {
            remoteEP.tcpSocket.Shutdown(SocketShutdown.Both);
            remoteEP.cancellationTokenSource.Cancel();
            remoteEP.tcpSocket.Close();
            connections.Remove(remoteEP.EndPoint);
            if(remoteEP.udpEndPoint != null) 
            {
                connectionsUDP.Remove(remoteEP.udpEndPoint);
            }
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
            int offset = 0;
            int size = buffer.Length;
            bool validPacket;

            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    NetPacket finalPacket;
                    (finalPacket, offset, size, validPacket) = await ReceiveTCPAsync(netEndPoint, receivedPacket, buffer, offset, size);
                    receivedPacket.Remove(0, offset);

                    if (!validPacket)
                    {
                        bool breakLoop = ProcessStatusPacket(finalPacket, netEndPoint);
                        if(breakLoop)
                            break;
                        else
                            continue;
                    }
                    
                    ReceivePacketOnMainThread(netEndPoint, finalPacket, PacketProtocol.TCP);
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

        private async Task<(NetPacket, int, int, bool)> ReceiveTCPAsync(NetEndPoint netEndPoint, NetPacket receivedPacket, byte[] buffer, int offset, int size, bool ignoreExpectedLength = false, bool inRecursiveCall = false)
        {
            NetPacket finalPacket = new NetPacket();
            ArraySegment<byte> segBuffer = new ArraySegment<byte>(buffer, offset, size);
            int expectedLength = 0;
            bool grabbedPacketLength = false;
            bool validPacket = false;

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                //Console.WriteLine("Recursion: " + inRecursiveCall);
                // If there are enough bytes to form an int
                if (receivedPacket.Length >= 4)
                {
                    // If we haven't already grabbed the packet length
                    if (!grabbedPacketLength && !ignoreExpectedLength)
                    {
                        try
                        {
                            expectedLength = receivedPacket.ReadInt();
                            Console.WriteLine("Expected Length: " + expectedLength);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            finalPacket.Write((int)DisconnectCode.InvalidPacket);
                            break;
                        }

                        // Connection status packets have an expected length of less than 0, so just return the final packet so it can be acted upon later
                        if (expectedLength < 0)
                        {
                            // If this is a recursive call, break out of the loop. We don't multiple levels of recursion
                            if (inRecursiveCall)
                            {
                                finalPacket.Write((int)DisconnectCode.InvalidPacket);
                                break;
                            }

                            finalPacket.Write(expectedLength);

                            if (expectedLength == UDPPortReceiveCode || expectedLength == (int)DisconnectCode.ConnectionClosedWithMessage)
                            {
                                // Receive the rest of the packet in a recursive call
                                //Console.WriteLine("Begin receiving TCP data RECURSIVE for " + netEndPoint.EndPoint.ToString());
                                var receivedData = await ReceiveTCPAsync(netEndPoint, receivedPacket, buffer, offset, size, false, true);
                                // If the packet is valid, write the rest of the data to the final packet, otherwise just write the invalid packet code
                                if (receivedData.Item4)
                                {
                                    finalPacket.Write(receivedData.Item1.ReadBytes(receivedData.Item1.UnreadLength));
                                }
                                else
                                {
                                    finalPacket.Clear();
                                    finalPacket.Write(receivedData.Item1.ReadInt());
                                }
                            }

                            break;
                        }
                        // If the expected length of the packet is greater than the set TCP buffer size
                        else if (expectedLength > TCP.BufferSize)
                        {
                            Console.WriteLine("Over Buffer Size: " + expectedLength);
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

                //Console.WriteLine("Begin receiving TCP data for " + netEndPoint.EndPoint.ToString());
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

            if (!inRecursiveCall)
            {
                Buffer.BlockCopy(buffer, receivedPacket.CurrentIndex, buffer, 0, receivedPacket.UnreadLength);
            }

            return (finalPacket, receivedPacket.CurrentIndex, segBuffer.Count, validPacket);
        }

        private async Task<NetPacket> ReceiveTCPAsync(NetEndPoint remoteEP, bool ignoreExpectedLength = false)
        {
            return (await ReceiveTCPAsync(remoteEP, new NetPacket(), new byte[TCP.MaxPacketSize], 0, TCP.MaxPacketSize, ignoreExpectedLength)).Item1;
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
                    receivedPacket.Clear();

                    if (!validPacket)
                    {
                        ProcessStatusPacket(finalPacket, netEndPoint);
                        break;
                    }

                    ReceivePacketOnMainThread(netEndPoint, finalPacket, PacketProtocol.UDP);
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

                    var data = segBuffer.Slice(4, receivedBytes.ReceivedBytes - 4);
                    finalPacket.Write(data.ToArray());
                    validPacket = true;
                    break;
                }
            }

            packetPool.Return(buffer);
            return (finalPacket, remoteEP, validPacket);
        }

        private bool ProcessStatusPacket(NetPacket finalPacket, NetEndPoint netEndPoint)
        {
            int code = finalPacket.ReadInt();

            if (code == UDPPortReceiveCode)
            {
                if (netEndPoint.udpEndPoint == null)
                {
                    int udpPort = finalPacket.ReadInt();
                    IPEndPoint udpEP = new IPEndPoint(netEndPoint.EndPoint.Address, udpPort);
                    NetEndPoint udpEndPoint = new NetEndPoint(udpEP, netEndPoint.tcpSocket, this);
                    netEndPoint.udpEndPoint = udpEP;
                    udpEndPoint.udpEndPoint = udpEP;
                    connectionsUDP.Add(udpEndPoint.EndPoint, udpEndPoint);
                    //Console.WriteLine("Status code: " + code);
                    return false;
                }
                else
                {
                    code = (int)DisconnectCode.InvalidPacket;
                }
            }

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
                if (finalPacket.UnreadLength > 0)
                    disconnect = new NetDisconnect((DisconnectCode)code, new NetPacket(finalPacket.ReadBytes(finalPacket.UnreadLength)));
                else
                    disconnect = new NetDisconnect((DisconnectCode)code);
            }
            else
            {
                disconnect = new NetDisconnect(DisconnectCode.InvalidPacket);
            }

            //Console.WriteLine("Disconnection code: " + disconnect.DisconnectCode.ToString());

            DisconnectOnMainThread(netEndPoint, disconnect, false);
            return true;
        }

        private void Reset(bool clearUnmanaged)
        {
            cancellationTokenSource.Cancel();

            if (clearUnmanaged)
            {
                //tcpSocket.Shutdown(SocketShutdown.Both);
                //udpSocket.Shutdown(SocketShutdown.Both);
                tcpSocket.Close();
                udpSocket.Close();
                cancellationTokenSource.Dispose();
            }

            connections.Clear();
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
                    connections = null;
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
