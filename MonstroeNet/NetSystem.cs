using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MonstroeNet
{
    public class NetSystem
    {
        public class Protocol
        {
            public int BufferSize { get; set; }
            public int MinPacketSize { get; set; }
            public int MaxPacketSize { get; set; }
        }

        enum PacketStatus
        {
            Disconnect = -1,
            DisconnectForcefully = -2,
            OverMaxPacketSize = -3,
            UnderMinPacketSize = -4,
            OverBufferSize = -5,
            Accept = 1,
            Deny = 0
        }

        public delegate void ConnectionRequestHandler(NetRequest request);
        public delegate void ConnectedHandler(NetEndPoint remoteEndPoint);
        public delegate void DisconnectedHandler(NetDisconnect disconnect);
        public delegate void PacketReceiveHandler(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol);
        public delegate void NetworkErrorHandler(SocketException socketException);

        public event ConnectionRequestHandler? OnConnectionRequest;
        public event ConnectedHandler? OnConnected;
        public event DisconnectedHandler? OnDisconnected;
        public event PacketReceiveHandler? OnPacketReceive;
        public event NetworkErrorHandler? OnNetworkError;

        public string Address { get; set; }
        public int Port { get; set; }
        public int MaxPendingConnections { get; set; }

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

        private Dictionary<IPEndPoint, NetEndPoint> connections;

        private Socket tcpSocket;
        private Socket udpSocket;

        private ConcurrentQueue<NetRequest> connectionRequestQueue;
        internal ConcurrentQueue<(bool, NetEndPoint)> connectionResultQueue;
        private ConcurrentQueue<NetDisconnect> disconnectionQueue;
        private ConcurrentQueue<NetEndPoint> beginReceiveQueue;
        private ConcurrentQueue<(NetEndPoint, NetPacket, PacketProtocol)> packetReceiveQueue;
        private ConcurrentQueue<SocketException> errorQueue;

        private CancellationTokenSource cancellationTokenSource;
        private ArrayPool<byte> packetPool;

        private byte[] udpBuffer;
        private ArraySegment<byte> udpSegBuffer;
        private NetPacket udpReceivedPacket;

        public NetSystem()
        {
            MaxPendingConnections = 100;

            TCP = new Protocol();
            UDP = new Protocol();

            TCP.BufferSize = 1024;
            TCP.MinPacketSize = 3;
            TCP.MaxPacketSize = 50;

            UDP.BufferSize = 4096;
            UDP.MinPacketSize = 3;
            UDP.MinPacketSize = 50;

            connections = new Dictionary<IPEndPoint, NetEndPoint>();

            connectionResultQueue = new ConcurrentQueue<(bool, NetEndPoint)>();
            beginReceiveQueue = new ConcurrentQueue<NetEndPoint>();
            packetReceiveQueue = new ConcurrentQueue<(NetEndPoint, NetPacket, PacketProtocol)>();
            errorQueue = new ConcurrentQueue<SocketException>();

            cancellationTokenSource = new CancellationTokenSource();
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

        public void Start()
        {
            IPEndPoint ep = new IPEndPoint(Address == null ? IPAddress.Any : IPAddress.Parse(Address), Port);
            tcpSocket = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            udpSocket = new Socket(ep.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        }

        public void RegisterInterface(IEventNetClient iClient)
        {
            OnConnected += iClient.OnConnected;
            OnDisconnected += iClient.OnDisconnected;
            OnPacketReceive += iClient.OnPacketReceive;
            OnNetworkError += iClient.OnNetworkError;
        }

        public void RegisterInterface(IEventNetListener iListener)
        {
            OnConnectionRequest += iListener.OnConnectionRequest;
            OnConnected += iListener.OnClientConnected;
            OnDisconnected += iListener.OnClientDisconnected;
            OnPacketReceive += iListener.OnPacketReceive;
            OnNetworkError += iListener.OnNetworkError;
        }

        public async void Connect()
        {
            // TODO: Check to prevent method from being called twice

            Mode = SystemMode.Client;

            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(Address), Port);
            NetEndPoint remoteEP = new NetEndPoint(ep, tcpSocket, this);

            bool tcpConnected = false;
            bool udpConnected = false;

            try
            {
                await tcpSocket.ConnectAsync(Address, Port);
                tcpConnected = true;
            }
            catch (SocketException ex) { errorQueue.Enqueue(ex); }

            try
            {
                await udpSocket.ConnectAsync(Address, Port);
                udpConnected = true;
            }
            catch (SocketException ex) { errorQueue.Enqueue(ex); }

            if (tcpConnected && udpConnected)
            {
                try
                {
                    connections.Add(ep, remoteEP);
                    var receivedData = await ReceiveTCPAsync(remoteEP, new NetPacket(), new byte[TCP.MaxPacketSize], 0, 4, cancellationTokenSource.IsCancellationRequested);
                    if(receivedData.Item1.ReadInt(false) == (int)PacketStatus.Accept)
                    {
                        OnConnected?.Invoke(remoteEP);
                        beginReceiveQueue.Enqueue(remoteEP);
                    }
                    else
                    {
                        NetDisconnect disconnect;
                        if(receivedData.Item1.ReadInt(false) == (int)PacketStatus.Deny)
                            disconnect = new NetDisconnect(DisconnectCode.ConnectionRejected);
                        else
                            disconnect = new NetDisconnect(DisconnectCode.InvalidPacket);
                        DisconnectForcefully(remoteEP, disconnect);
                    }
                }
                catch (SocketException ex)
                {
                    errorQueue.Enqueue(ex);
                    DisconnectForcefully(remoteEP, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode));
                }
            }
        }

        public void Listen()
        {
            // TODO: Check to prevent method from being called twice

            Mode = SystemMode.Listener;

            IPEndPoint ep = new IPEndPoint(IPAddress.Any, Port);
            tcpSocket.Bind(ep);
            udpSocket.Bind(ep);

            tcpSocket.Listen(MaxPendingConnections);
            //udpSocket.Listen(MaxPendingConnections);

            AcceptClients();
            ReceivePackets();
        }

        private void AcceptClients()
        {
            connectionRequestQueue = new ConcurrentQueue<NetRequest>();

            Task.Run(async () =>
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        Socket clientTcpSock = await tcpSocket.AcceptAsync();
                        NetEndPoint clientEP = new NetEndPoint((IPEndPoint)clientTcpSock.RemoteEndPoint, clientTcpSock, this);
                        NetRequest request = new NetRequest(clientEP, this);
                        connectionRequestQueue.Enqueue(request);
                    }
                    catch (SocketException ex)
                    {
                        errorQueue.Enqueue(ex);
                    }
                }
            }, cancellationTokenSource.Token);
        }

        private void ReceivePackets()
        {
            Task.Run(() => ReceiveUDPAsync(), cancellationTokenSource.Token);
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

        public async void Send(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol)
        {
            packet.InsertLength();
            await SendPacket(remoteEP, packet, protocol);
        }

        private async Task<bool> SendPacket(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol)
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
                errorQueue.Enqueue(ex);
                DisconnectForcefully(remoteEP, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode));
            }

            return returnValue;
        }

        public async void Disconnect(NetEndPoint remoteEP)
        {
            await DisconnectSafely(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosedSafely));
        }

        public async void Disconnect(NetEndPoint remoteEP, NetPacket disconnectPacket)
        {
            await DisconnectSafely(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosedSafely, disconnectPacket));
        }

        private async Task<bool> DisconnectSafely(NetEndPoint remoteEP, NetDisconnect? disconnect = null, DisconnectCode code = DisconnectCode.ConnectionClosedSafely)
        {
            bool returnValue = false;
            using (NetPacket packet = new NetPacket())
            {
                try
                {
                    packet.Write((int)code);
                    ArraySegment<byte> segBuffer = new ArraySegment<byte>(packet.ByteArray);
                    await remoteEP.tcpSocket.SendAsync(segBuffer, SocketFlags.None);
                    returnValue = true;
                }
                catch (SocketException ex)
                {
                    errorQueue.Enqueue(ex);
                }
                DisconnectForcefully(remoteEP, disconnect);
            }

            return returnValue;
        }

        public void DisconnectForcefully(NetEndPoint remoteEP)
        {
            DisconnectForcefully(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosedForcefully));
        }

        private void DisconnectForcefully(NetEndPoint remoteEP, NetDisconnect? disconnect = null)
        {
            remoteEP.tcpSocket.Close();
            connections.Remove(remoteEP.EndPoint);
            
            if(disconnect != null)
            {
                disconnectionQueue.Enqueue(disconnect);
            }
        }

        public async void Close()
        {
            foreach (var remoteEP in connections.Values)
            {
                await DisconnectSafely(remoteEP);
            }

            Reset();
        }

        private void Reset()
        {
            tcpSocket.Close();
            udpSocket.Close();

            // Reset Everything else
        }

        private async void HandleError(NetEndPoint netEndPoint, SocketException ex)
        {
            errorQueue.Enqueue(ex);
            await DisconnectSafely(netEndPoint, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode), DisconnectCode.SocketError);
        }

        private async void SendRequestAccept(NetEndPoint remoteEP)
        {
            using (NetPacket packet = new NetPacket())
            {
                packet.Write((int)PacketStatus.Accept);
                await SendPacket(remoteEP, packet, PacketProtocol.TCP);
            }
        }

        private async void SendRequestDeny(NetEndPoint remoteEP)
        {
            using (NetPacket packet = new NetPacket())
            {
                packet.Write((int)PacketStatus.Deny);
                if(await SendPacket(remoteEP, packet, PacketProtocol.TCP))
                {
                    DisconnectForcefully(remoteEP, new NetDisconnect(DisconnectCode.ConnectionRejected));
                }
            }
        }

        public void Update()
        {
            if (Mode == SystemMode.Listener)
            {
                PollConnectionRequest();
                PollConnectionResult();
            }

            PollDisconnection();
            PollPacketsReceived();
            PollErrors();
        }

        private void PollConnectionRequest()
        {
            if (connectionRequestQueue.TryDequeue(out var result))
            {
                OnConnectionRequest?.Invoke(result);
            }
        }

        private void PollConnectionResult()
        {
            if(connectionResultQueue.TryDequeue(out var result))
            {
                connections.Add(result.Item2.EndPoint, result.Item2);
                if (result.Item1)
                {
                    OnConnected?.Invoke(result.Item2);
                    beginReceiveQueue.Enqueue(result.Item2);
                    SendRequestAccept(result.Item2);
                }
                else
                {
                    SendRequestDeny(result.Item2);
                }
            }
        }

        private void PollDisconnection()
        {
            if(disconnectionQueue.TryDequeue(out var result))
            {
                OnDisconnected?.Invoke(result);
            }
        }

        private void PollPacketsReceived()
        {
            if (packetReceiveQueue.TryDequeue(out var result))
            {
                OnPacketReceive?.Invoke(result.Item1, result.Item2, result.Item3);
            }
        }

        private void PollErrors()
        {
            if(errorQueue.TryDequeue(out var result))
            {
                OnNetworkError?.Invoke(result);
            }
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
                    (finalPacket, offset, size, validPacket) = await ReceiveTCPAsync(netEndPoint, receivedPacket, buffer, offset, size, cancellationTokenSource.IsCancellationRequested);
                    
                    if(!validPacket)
                    {
                        int disconnectCode = finalPacket.ReadInt();
                        bool containsCode = new List<int>((IEnumerable<int>)Enum.GetValues(typeof(DisconnectCode))).Contains(disconnectCode);
                        if (containsCode)
                            await DisconnectSafely(netEndPoint, new NetDisconnect((DisconnectCode)finalPacket.ReadInt(false)), (DisconnectCode)finalPacket.ReadInt(false));
                        else
                            await DisconnectSafely(netEndPoint, new NetDisconnect(DisconnectCode.InvalidPacket), DisconnectCode.InvalidPacket);
                        break;
                    }

                    receivedPacket.Clear();
                    packetReceiveQueue.Enqueue((netEndPoint, finalPacket, PacketProtocol.TCP));
                }
            }
            catch (SocketException ex)
            {
                errorQueue.Enqueue(ex);
                await DisconnectSafely(netEndPoint, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode), DisconnectCode.SocketError);
            }

            receivedPacket.Dispose();
            packetPool.Return(buffer);
        }

        private async Task<(NetPacket, int, int, bool)> ReceiveTCPAsync(NetEndPoint netEndPoint, NetPacket receivedPacket, byte[] buffer, int offset, int size, bool cancellationRequested)
        {
            NetPacket finalPacket = new NetPacket();
            ArraySegment<byte> segBuffer = new ArraySegment<byte>(buffer, offset, size);
            int packetOffset = 0;
            int expectedLength = 0;
            bool grabbedPacketLength = false;
            bool validPacket = false;
            bool packetComplete = false;

            while (!cancellationRequested)
            {
                var receivedBytes = await netEndPoint.tcpSocket.ReceiveAsync(segBuffer, SocketFlags.None);

                // If a completely blank packet was sent
                if (receivedBytes == 0)
                {
                    finalPacket.Write((int)DisconnectCode.ConnectionClosedForcefully);
                    packetComplete = true;
                }

                var data = segBuffer.Slice(packetOffset, receivedBytes + segBuffer.Offset - packetOffset);
                receivedPacket.Write(data.ToArray());

                // If there are enough bytes to form an int
                if (receivedPacket.Length > 4 && !packetComplete)
                {
                    // If we haven't already grabbed the packet length
                    if (!grabbedPacketLength)
                    {
                        expectedLength = receivedPacket.ReadInt();
                        grabbedPacketLength = true;
                    }

                    packetComplete = true;
                    // Connection status packets have an expected length of less than 0, so just return the final packet so it can be acted upon later
                    if (expectedLength < 0)
                        finalPacket.Write(expectedLength);
                    // If the expected length of the packet is greater than the set TCP buffer size
                    else if (expectedLength > TCP.BufferSize)
                        finalPacket.Write((int)DisconnectCode.PacketOverBufferSize);
                    // If the expected length of the packet is greater than the set max packet size
                    else if (expectedLength > TCP.MaxPacketSize)
                        finalPacket.Write((int)DisconnectCode.PacketOverMaxSize);
                    // If the expected length of the packet is less than the set min packet size
                    else if (expectedLength < TCP.MinPacketSize)
                        finalPacket.Write((int)DisconnectCode.PacketUnderMinSize);
                    // If all the bytes in the packet have been received
                    else if (expectedLength <= receivedPacket.UnreadLength)
                    {
                        finalPacket.Write(receivedPacket.ReadBytes(expectedLength));
                        validPacket = true;
                    }
                    else
                        packetComplete = false;
                }

                // If the final packet has been built
                if (packetComplete)
                {
                    segBuffer = segBuffer.Slice(segBuffer.Offset + finalPacket.Length, receivedPacket.UnreadLength);
                    break;
                }

                segBuffer = segBuffer.Slice(segBuffer.Offset + receivedBytes);
                packetOffset = segBuffer.Offset;
            }

            Buffer.BlockCopy(buffer, segBuffer.Offset, buffer, 0, segBuffer.Count);
            //segBuffer = segBuffer.Slice(segBuffer.Count);
            //receivedPacket.Clear();

            return (finalPacket, segBuffer.Count, buffer.Length - segBuffer.Count, validPacket);
        }

        private async void StartReceivingUDP()
        {
            byte[] buffer = packetPool.Rent(UDP.BufferSize);
            NetPacket receivedPacket = new NetPacket();

            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    NetPacket finalPacket = await ReceiveUDPAsync(receivedPacket, !cancellationTokenSource.IsCancellationRequested);
                    receivedPacket.Clear();

                    packetReceiveQueue.Enqueue((netEndPoint, finalPacket, PacketProtocol.TCP));
                }
            }
            catch (SocketException ex)
            {
                errorQueue.Enqueue(ex);
                await DisconnectSafely(netEndPoint, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode), DisconnectCode.SocketError);
            }

            receivedPacket.Dispose();
            packetPool.Return(buffer);
        }

        private async Task<(NetPacket, NetEndPoint)> ReceiveUDPAsync(NetPacket receivedPacket, bool cancellationRequested)
        {
            NetPacket finalPacket = new NetPacket();
            NetEndPoint remoteEP;
            byte[] buffer = packetPool.Rent(UDP.BufferSize);
            ArraySegment<byte> segBuffer;

            while (!cancellationRequested)
            {
                packetPool.Return(buffer);
                buffer = packetPool.Rent(UDP.BufferSize);
                segBuffer = new ArraySegment<byte>(buffer);

                var receivedBytes = await udpSocket.ReceiveFromAsync(segBuffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));
                
                // If the received data is from a currently connected end point
                if (connections.TryGetValue((IPEndPoint)receivedBytes.RemoteEndPoint, out remoteEP))
                {
                    // Disregard packet as it is too small
                    if (receivedBytes.ReceivedBytes < 4)
                    {
                        continue;
                    }

                    receivedPacket.Write(segBuffer.Slice(0, 4).ToArray());
                    int expectedLength = receivedPacket.ReadInt();

                    // Partial packet received, disregard
                    if (expectedLength > receivedBytes.ReceivedBytes)
                    {
                        continue;
                    }

                    // Packets expected length was smaller than the actual amount of bytes received
                    if (expectedLength < receivedBytes.ReceivedBytes)
                        finalPacket.Write((int)DisconnectCode.InvalidPacket);
                    // If the expected length of the packet is greater than the set UDP buffer size
                    else if (expectedLength > UDP.BufferSize)
                        finalPacket.Write((int)DisconnectCode.PacketOverBufferSize);
                    // If the expected length of the packet is greater than the set max packet size
                    else if (expectedLength > UDP.MaxPacketSize)
                        finalPacket.Write((int)DisconnectCode.PacketOverMaxSize);
                    // If the expected length of the packet is less than the set min packet size
                    else if (expectedLength < UDP.MinPacketSize)
                        finalPacket.Write((int)DisconnectCode.PacketUnderMinSize);

                    // If one of the errors above occured
                    if (finalPacket.Length > 0)
                    {
                        break;
                    }

                    var data = segBuffer.Slice(4, receivedBytes.ReceivedBytes - 4);

                    //NetEndPoint remoteEP = new NetEndPoint((IPEndPoint)receivedBytes.RemoteEndPoint);
                    if (connections.TryGetValue((IPEndPoint)receivedBytes.RemoteEndPoint, out var remoteEndPoint))
                    {
                        NetPacket finalPacket = new NetPacket(data.ToArray());
                        packetReceiveQueue.Enqueue((remoteEndPoint, finalPacket, PacketProtocol.UDP));
                    }
                    else
                    {
                        // TODO: Send Malicious Error
                    }
                }
            }
        }

        //private async Task<NetPacket> ReceiveTCPAsync(NetEndPoint netEndPoint)
        //{
        //    byte[] buffer = packetPool.Rent(TCP.BufferSize);
        //    ArraySegment<byte> segBuffer = new ArraySegment<byte>(buffer);
        //    NetPacket receivedPacket = new NetPacket();

        //    while (!cancellationTokenSource.IsCancellationRequested)
        //    {
        //        int packetOffset = 0;
        //        int expectedLength = 0;
        //        bool grabbedPacketLength = false;

        //        try
        //        {
        //            while (!cancellationTokenSource.IsCancellationRequested)
        //            {
        //                //var receivedBytes = await tcpSocket.ReceiveAsync(segBuffer, SocketFlags.None, netEndPoint.EndPoint);
        //                var receivedBytes = await netEndPoint.tcpSocket.ReceiveAsync(segBuffer, SocketFlags.None);

        //                // If a completely blank packet was sent
        //                if (receivedBytes/*.ReceivedBytes*/ == 0)
        //                {
        //                    // TODO: Send Malicious Error
        //                    DisconnectForcefully(netEndPoint);
        //                    break;
        //                }

        //                var data = segBuffer.Slice(packetOffset, receivedBytes/*.ReceivedBytes*/ + segBuffer.Offset - packetOffset);
        //                receivedPacket.Write(data.ToArray());

        //                // If there are enough bytes to form an int
        //                if (receivedPacket.Length > 4)
        //                {
        //                    // If we haven't already grabbed the packet length
        //                    if (!grabbedPacketLength)
        //                    {
        //                        expectedLength = receivedPacket.ReadInt();
        //                        grabbedPacketLength = true;
        //                    }

        //                    if (expectedLength == -1)
        //                    {
        //                        DisconnectForcefully(netEndPoint);
        //                        break;
        //                    }

        //                    // If the expected length of the packet is less than or equal to 0
        //                    if (expectedLength < TCP.MinPacketSize)
        //                    {
        //                        // TODO: Send Malicious Error
        //                        break;
        //                    }
        //                    if (expectedLength > TCP.MaxPacketSize)
        //                    {
        //                        // TODO: Send Malicious Error
        //                        break;
        //                    }
        //                    if (expectedLength > TCP.BufferSize)
        //                    {
        //                        // TODO: Send Malicious Error
        //                        break;
        //                    }

        //                    // If all the bytes in the packet have been received
        //                    if (expectedLength <= receivedPacket.UnreadLength)
        //                    {
        //                        NetPacket finalPacket = new NetPacket(receivedPacket.ReadBytes(expectedLength));
        //                        packetReceiveQueue.Enqueue((netEndPoint, finalPacket, PacketProtocol.TCP));
        //                        segBuffer = segBuffer.Slice(segBuffer.Offset + finalPacket.Length, receivedPacket.UnreadLength);
        //                        break;
        //                    }

        //                }

        //                segBuffer = segBuffer.Slice(segBuffer.Offset + receivedBytes/*.ReceivedBytes*/);
        //                packetOffset = segBuffer.Offset;
        //            }

        //            Buffer.BlockCopy(buffer, segBuffer.Offset, buffer, 0, segBuffer.Count);
        //            segBuffer.Slice(segBuffer.Count);
        //            receivedPacket.Clear();
        //        }
        //        catch (SocketException ex)
        //        {
        //            errorQueue.Enqueue(ex);
        //            Disconnect(netEndPoint);
        //        }
        //    }

        //    receivedPacket.Dispose();
        //}

        //private async void ReceiveTCPAsync(NetEndPoint netEndPoint)
        //{
        //    byte[] buffer = packetPool.Rent(TCP.BufferSize);
        //    ArraySegment<byte> segBuffer = new ArraySegment<byte>(buffer);
        //    NetPacket receivedPacket = new NetPacket();

        //    while (!cancellationTokenSource.IsCancellationRequested)
        //    {
        //        int packetOffset = 0;
        //        int expectedLength = 0;
        //        bool grabbedPacketLength = false;

        //        try
        //        {
        //            while (!cancellationTokenSource.IsCancellationRequested)
        //            {
        //                //var receivedBytes = await tcpSocket.ReceiveAsync(segBuffer, SocketFlags.None, netEndPoint.EndPoint);
        //                var receivedBytes = await netEndPoint.tcpSocket.ReceiveAsync(segBuffer, SocketFlags.None);

        //                // If a completely blank packet was sent
        //                if (receivedBytes/*.ReceivedBytes*/ == 0)
        //                {
        //                    // TODO: Send Malicious Error
        //                    DisconnectForcefully(netEndPoint);
        //                    break;
        //                }

        //                var data = segBuffer.Slice(packetOffset, receivedBytes/*.ReceivedBytes*/ + segBuffer.Offset - packetOffset);
        //                receivedPacket.Write(data.ToArray());

        //                // If there are enough bytes to form an int
        //                if (receivedPacket.Length > 4)
        //                {
        //                    // If we haven't already grabbed the packet length
        //                    if (!grabbedPacketLength)
        //                    {
        //                        expectedLength = receivedPacket.ReadInt();
        //                        grabbedPacketLength = true;
        //                    }

        //                    if (expectedLength == -1)
        //                    {
        //                        DisconnectForcefully(netEndPoint);
        //                        break;
        //                    }

        //                    // If the expected length of the packet is less than or equal to 0
        //                    if (expectedLength < TCP.MinPacketSize)
        //                    {
        //                        // TODO: Send Malicious Error
        //                        break;
        //                    }
        //                    if (expectedLength > TCP.MaxPacketSize)
        //                    {
        //                        // TODO: Send Malicious Error
        //                        break;
        //                    }
        //                    if (expectedLength > TCP.BufferSize)
        //                    {
        //                        // TODO: Send Malicious Error
        //                        break;
        //                    }

        //                    // If all the bytes in the packet have been received
        //                    if (expectedLength <= receivedPacket.UnreadLength)
        //                    {
        //                        NetPacket finalPacket = new NetPacket(receivedPacket.ReadBytes(expectedLength));
        //                        packetReceiveQueue.Enqueue((netEndPoint, finalPacket, PacketProtocol.TCP));
        //                        segBuffer = segBuffer.Slice(segBuffer.Offset + finalPacket.Length, receivedPacket.UnreadLength);
        //                        break;
        //                    }

        //                }

        //                segBuffer = segBuffer.Slice(segBuffer.Offset + receivedBytes/*.ReceivedBytes*/);
        //                packetOffset = segBuffer.Offset;
        //            }

        //            Buffer.BlockCopy(buffer, segBuffer.Offset, buffer, 0, segBuffer.Count);
        //            segBuffer.Slice(segBuffer.Count);
        //            receivedPacket.Clear();
        //        }
        //        catch (SocketException ex)
        //        {
        //            errorQueue.Enqueue(ex);
        //            Disconnect(netEndPoint);
        //        }
        //    }

        //    receivedPacket.Dispose();
        //}

        //private async void ReceiveUDPAsync()
        //{
        //    byte[] buffer = packetPool.Rent(UDP.BufferSize);
        //    ArraySegment<byte> segBuffer = new ArraySegment<byte>(buffer);
        //    NetPacket receivedPacket = new NetPacket();

        //    while (!cancellationTokenSource.IsCancellationRequested)
        //    {
        //        try
        //        {
        //            while(!cancellationTokenSource.IsCancellationRequested)
        //            {
        //                var receivedBytes = await udpSocket.ReceiveFromAsync(segBuffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));

        //                // If a completely blank packet was sent
        //                if (receivedBytes.ReceivedBytes == 0)
        //                {
        //                    DisconnectForcefully(connections[(IPEndPoint)receivedBytes.RemoteEndPoint]);
        //                    break;
        //                }

        //                // Disregard packet as it is too small
        //                if (receivedBytes.ReceivedBytes < 4)
        //                {
        //                    continue;
        //                }

        //                receivedPacket.Write(segBuffer.Slice(0, 4).ToArray());
        //                int expectedLength = receivedPacket.ReadInt();

        //                // Partial packet received, disregard
        //                if (expectedLength > receivedBytes.ReceivedBytes)
        //                {
        //                    continue;
        //                }

        //                // Packets expected length was smaller than the actual amount of bytes received
        //                if (expectedLength < receivedBytes.ReceivedBytes)
        //                {
        //                    // TODO: Send Error
        //                    break;
        //                }

        //                // If the expected length of the packet is less than or equal to 0
        //                if (expectedLength < UDP.MinPacketSize)
        //                {
        //                    // TODO: Send Malicious Error
        //                    break;
        //                }
        //                if (expectedLength > UDP.MaxPacketSize)
        //                {
        //                    // TODO: Send Malicious Error
        //                    break;
        //                }
        //                if (expectedLength > UDP.BufferSize)
        //                {
        //                    // TODO: Send Malicious Error
        //                    break;
        //                }

        //                var data = segBuffer.Slice(4, receivedBytes.ReceivedBytes - 4);

        //                //NetEndPoint remoteEP = new NetEndPoint((IPEndPoint)receivedBytes.RemoteEndPoint);
        //                if (connections.TryGetValue((IPEndPoint)receivedBytes.RemoteEndPoint, out var remoteEndPoint))
        //                {
        //                    NetPacket finalPacket = new NetPacket(data.ToArray());
        //                    packetReceiveQueue.Enqueue((remoteEndPoint, finalPacket, PacketProtocol.UDP));
        //                }
        //                else
        //                {
        //                    // TODO: Send Malicious Error
        //                }

        //                receivedPacket.Clear();
        //            }
        //        }
        //        catch (SocketException ex)
        //        {
        //            // TODO: Disconnect from server
        //            errorQueue.Enqueue(ex);
        //        }
        //    }

        //    receivedPacket.Dispose();
        //}

    }
}
