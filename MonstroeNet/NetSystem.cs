using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MonstroeNet
{
    public class Protocol
    {
        public int BufferSize { get; set; }
        public int MinPacketSize { get; set; }
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
        private bool systemConnected;

        private ConcurrentQueue<NetRequest> connectionRequestQueue;
        internal ConcurrentQueue<(bool, NetEndPoint)> connectionResultQueue;
        private ConcurrentQueue<(NetEndPoint, NetDisconnect, bool)> disconnectionQueue;
        private ConcurrentQueue<NetEndPoint> beginReceiveQueue;
        private ConcurrentQueue<(NetEndPoint, NetPacket, PacketProtocol)> packetReceiveQueue;
        private ConcurrentQueue<(NetEndPoint, NetPacket, PacketProtocol)> packetSendQueue;
        private ConcurrentQueue<SocketException> errorQueue;

        private CancellationTokenSource cancellationTokenSource;
        private ArrayPool<byte> packetPool;

        public NetSystem()
        {
            MaxPendingConnections = 100;

            TCP = new Protocol();
            UDP = new Protocol();

            TCP.BufferSize = 1024;
            TCP.MinPacketSize = 4;
            TCP.MaxPacketSize = 50;

            UDP.BufferSize = 4096;
            UDP.MinPacketSize = 4;
            UDP.MinPacketSize = 50;

            connections = new Dictionary<IPEndPoint, NetEndPoint>();
            systemConnected = false;

            connectionRequestQueue = new ConcurrentQueue<NetRequest>();
            connectionResultQueue = new ConcurrentQueue<(bool, NetEndPoint)>();
            disconnectionQueue = new ConcurrentQueue<(NetEndPoint, NetDisconnect, bool)>();
            beginReceiveQueue = new ConcurrentQueue<NetEndPoint>();
            packetReceiveQueue = new ConcurrentQueue<(NetEndPoint, NetPacket, PacketProtocol)>();
            packetSendQueue = new ConcurrentQueue<(NetEndPoint, NetPacket, PacketProtocol)>();
            errorQueue = new ConcurrentQueue<SocketException>();

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

        private void InitSockets()
        {
            IPEndPoint ep = new IPEndPoint(Address == null ? IPAddress.Any : IPAddress.Parse(Address), Port);

            //if (tcpSocket != null)
            //    tcpSocket.Close();
            //if (udpSocket != null)
            //    udpSocket.Close();
            //if (cancellationTokenSource != null)// && cancellationTokenSource.IsCancellationRequested)
            //    cancellationTokenSource.Dispose();
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
            if (systemConnected)
            {
                throw new InvalidOperationException("Please call 'Close' before calling 'Connect' again.");
            }

            systemConnected = true;
            InitSockets();
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
                    var receivedData = await ReceiveTCPAsync(remoteEP, new NetPacket(), new byte[TCP.MaxPacketSize], 0, TCP.MaxPacketSize, true);
                    if (receivedData.Item1.ReadInt(false) == (int)RequestStatus.Accept)
                    {
                        ReceivePackets();
                        beginReceiveQueue.Enqueue(remoteEP);
                        OnConnected?.Invoke(remoteEP);
                    }
                    else
                    {
                        NetDisconnect disconnect;
                        if (receivedData.Item1.ReadInt(false) == (int)RequestStatus.Deny)
                            disconnect = new NetDisconnect(DisconnectCode.ConnectionRejected);
                        else
                            disconnect = new NetDisconnect(DisconnectCode.InvalidPacket);
                        await DisconnectInternal(remoteEP, disconnect, false);
                    }
                }
                catch (SocketException ex)
                {
                    errorQueue.Enqueue(ex);
                    await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode), false);
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

            systemConnected = true;
            InitSockets();
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
                    catch (ObjectDisposedException) { Console.WriteLine("Object 'tcpSocket' disposed."); /* Catch this error when 'Close' is called */ }
                }
            }, cancellationTokenSource.Token);
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
                throw new Exception("Packets cannot be larger than " + (protocol == PacketProtocol.TCP ? "TCP." : "UDP") + " 'MaxPacketSize'.");
            }

            packet.InsertLength();
            packetSendQueue.Enqueue((remoteEP, packet, protocol));
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
                errorQueue.Enqueue(ex);
                if (disconnectOnError)
                {
                    Disconnect(remoteEP, new NetDisconnect(DisconnectCode.SocketError, ex.SocketErrorCode), false);
                }
            }

            return returnValue;
        }

        public void Disconnect(NetEndPoint remoteEP)
        {
            Disconnect(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosed), true);
        }

        public void Disconnect(NetEndPoint remoteEP, NetPacket disconnectPacket)
        {
            Disconnect(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosedWithMessage, disconnectPacket), true);
        }

        public void DisconnectForcefully(NetEndPoint remoteEP)
        {
            Disconnect(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosed), false);
        }

        private void Disconnect(NetEndPoint remoteEP, NetDisconnect disconnect, bool sendDisconnectPacketToRemote)
        {
            disconnectionQueue.Enqueue((remoteEP, disconnect, sendDisconnectPacketToRemote));
        }

        private async Task<bool> DisconnectInternal(NetEndPoint remoteEP, NetDisconnect disconnect, bool sendDisconnectPacketToRemote)
        {
            bool returnValue = true;

            if (sendDisconnectPacketToRemote)
            {
                using (NetPacket packet = new NetPacket())
                {
                    if (disconnect.DisconnectData != null)
                    {
                        packet.Write(disconnect.DisconnectData.ByteArray);
                        packet.InsertLength();
                    }
                    packet.Write((int)disconnect.DisconnectCode);
                    returnValue = await SendInternal(remoteEP, packet, PacketProtocol.TCP, false);
                }
            }

            CloseRemote(remoteEP, sendDisconnectPacketToRemote);
            return returnValue;
        }

        //public async void Disconnect(NetEndPoint remoteEP, NetPacket disconnectPacket)
        //{
        //    await DisconnectSafely(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosedSafely, disconnectPacket));
        //}

        /*private async Task<bool> DisconnectSafely(NetEndPoint remoteEP, NetDisconnect disconnect)
        {
            bool returnValue = false;
            using (NetPacket packet = new NetPacket())
            {
                packet.Write((int)disconnect.DisconnectCode);
                if(await SendPacket(remoteEP, packet, PacketProtocol.TCP))
                {
                    DisconnectForcefully(remoteEP, disconnect);
                }
                /ry
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
        }*/

        /*private void DisconnectForcefully(NetEndPoint remoteEP)//, NetDisconnect disconnect)
        {
            remoteEP.tcpSocket.Close();
            connections.Remove(remoteEP.EndPoint);
            //remoteEP.cancellationTokenSource.Cancel();

            //if(disconnect != null)
            //{
            //    disconnectionQueue.Enqueue((remoteEP, disconnect));
            //}

            //OnDisconnected?.Invoke(remoteEP, disconnect);
        }*/

        public async void Close(bool sendDisconnectPacketToRemote)
        {
            foreach (var remoteEP in connections.Values)
            {
                //await DisconnectSafely(remoteEP);
                //Disconnect(remoteEP, new NetDisconnect(DisconnectCode.HostClosed), true);
                await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectCode.ConnectionClosed), sendDisconnectPacketToRemote);
                //CloseRemoteEndpoint(remoteEP);
            }

            Reset(true);
        }

        private void CloseRemote(NetEndPoint remoteEP, bool sendZeroBytePacket)
        {
            //Task.Delay(10000);
            //remoteEP.tcpSocket.Shutdown(SocketShutdown.Both);
            remoteEP.cancellationTokenSource.Cancel();
            if(sendZeroBytePacket) 
            {
                
            }
            remoteEP.tcpSocket.Disconnect(false);
            remoteEP.tcpSocket.Close();
            //remoteEP.tcpSocket.Dispose();
            connections.Remove(remoteEP.EndPoint);
        }

        private async Task SendRequestAccept(NetEndPoint remoteEP)
        {
            using (NetPacket packet = new NetPacket())
            {
                packet.Write((int)RequestStatus.Accept);
                //packet.Write((int)RequestStatus.Accept);
                await SendInternal(remoteEP, packet, PacketProtocol.TCP);
            }
        }

        private async Task SendRequestDeny(NetEndPoint remoteEP)
        {
            using (NetPacket packet = new NetPacket())
            {
                packet.Write((int)RequestStatus.Deny);
                //packet.Write((int)RequestStatus.Deny);
                await SendInternal(remoteEP, packet, PacketProtocol.TCP, false);
                await DisconnectInternal(remoteEP, new NetDisconnect(DisconnectCode.ConnectionRejected), false);
            }
        }

        public void Update()
        {
            if (Mode == SystemMode.Listener)
            {
                PollConnectionRequest();
                PollConnectionResult();
            }

            PollErrors();
            PollPacketsSent();
            PollPacketsReceived();
            PollDisconnection();
        }

        private void PollConnectionRequest()
        {
            if (connectionRequestQueue.TryDequeue(out var result))
            {
                OnConnectionRequest?.Invoke(result);
            }
        }

        private async void PollConnectionResult()
        {
            if (connectionResultQueue.TryDequeue(out var result))
            {
                connections.Add(result.Item2.EndPoint, result.Item2);
                if (result.Item1)
                {
                    await SendRequestAccept(result.Item2);
                    beginReceiveQueue.Enqueue(result.Item2);
                    OnConnected?.Invoke(result.Item2);
                }
                else
                {
                    await SendRequestDeny(result.Item2);
                }
            }
        }

        private async void PollDisconnection()
        {
            if (disconnectionQueue.TryDequeue(out var result))
            {
                await DisconnectInternal(result.Item1, result.Item2, result.Item3);
                OnDisconnected?.Invoke(result.Item1, result.Item2);
            }
        }

        public async void PollPacketsSent()
        {
            if (packetSendQueue.TryDequeue(out var result))
            {
                await SendInternal(result.Item1, result.Item2, result.Item3);
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
            if (errorQueue.TryDequeue(out var result))
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
                    (finalPacket, offset, size, validPacket) = await ReceiveTCPAsync(netEndPoint, receivedPacket, buffer, offset, size);

                    if (!validPacket)
                    {
                        ProcessStatusPacket(finalPacket, netEndPoint);
                        break;
                    }

                    //receivedPacket.Clear();
                    receivedPacket.Remove(0, offset);
                    packetReceiveQueue.Enqueue((netEndPoint, finalPacket, PacketProtocol.TCP));
                }
            }
            catch (SocketException ex)
            {
                errorQueue.Enqueue(ex);
                Disconnect(netEndPoint, new NetDisconnect(DisconnectCode.SocketError), false);
            }
            //catch (ObjectDisposedException) { Console.WriteLine("Object netEndPoint.tcpSocket disposed."); /* Catch this error when 'Close' is called */ }
            catch (OperationCanceledException) { Console.WriteLine("Receiving Operation canceled."); }

            receivedPacket.Dispose();
            packetPool.Return(buffer);
        }

        private async Task<(NetPacket, int, int, bool)> ReceiveTCPAsync(NetEndPoint netEndPoint, NetPacket receivedPacket, byte[] buffer, int offset, int size, bool ignoreExpectedLength = false)
        {
            NetPacket finalPacket = new NetPacket();
            ArraySegment<byte> segBuffer = new ArraySegment<byte>(buffer, offset, size);
            //int packetOffset = 0;
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
                            if (expectedLength == (int)DisconnectCode.ConnectionClosedWithMessage)
                                expectedLength = receivedPacket.ReadInt();
                            else break;
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
                        //segBuffer = segBuffer.Slice(segBuffer.Offset + finalPacket.Length, receivedPacket.UnreadLength);
                        segBuffer = segBuffer.Slice(segBuffer.Offset, finalPacket.Length);
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

                //var data = segBuffer.Slice(packetOffset, receivedBytes + segBuffer.Offset - packetOffset);
                //receivedPacket.Write(data.ToArray());

                receivedPacket.Write(segBuffer.Slice(segBuffer.Offset, receivedBytes).ToArray());
                segBuffer = segBuffer.Slice(segBuffer.Offset + receivedBytes);
                //packetOffset = segBuffer.Offset;
            }

            if (validPacket)
            {
                Buffer.BlockCopy(buffer, segBuffer.Offset, buffer, 0, segBuffer.Count);
            }

            return (finalPacket, segBuffer.Count, buffer.Length - segBuffer.Count, validPacket);
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
                        ProcessStatusPacket(finalPacket, netEndPoint);
                        break;
                    }

                    receivedPacket.Clear();
                    packetReceiveQueue.Enqueue((netEndPoint, finalPacket, PacketProtocol.UDP));
                }
                catch (SocketException ex)
                {
                    errorQueue.Enqueue(ex);
                }
                catch (ObjectDisposedException) { Console.WriteLine("Object udpSocket disposed."); /* Catch this error when 'Close' is called */ }
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
                if (connections.TryGetValue((IPEndPoint)receivedBytes.RemoteEndPoint, out remoteEP))
                {
                    receivedPacket.Write(segBuffer.Slice(0, 4).ToArray());
                    int expectedLength = receivedPacket.ReadInt();

                    // Partial packet received, disregard
                    if (expectedLength > receivedBytes.ReceivedBytes)
                    {
                        continue;
                    }

                    // Packets expected length was smaller than the actual amount of bytes received
                    if (expectedLength < receivedBytes.ReceivedBytes)
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

        private void ProcessStatusPacket(NetPacket finalPacket, NetEndPoint netEndPoint)
        {
            int disconnectCode = finalPacket.ReadInt();
            //bool containsCode = new List<int>((IEnumerable<int>)Enum.GetValues(typeof(DisconnectCode))).Contains(disconnectCode);
            bool containsCode = false;
            foreach(int code in Enum.GetValues(typeof(DisconnectCode)))
            {
                if (code == disconnectCode)
                {
                    containsCode = true;
                    break;
                }
            }
            
            NetDisconnect disconnect;
            if (containsCode)
            {
                if (finalPacket.UnreadLength > 0)
                    disconnect = new NetDisconnect((DisconnectCode)disconnectCode, new NetPacket(finalPacket.ReadBytes(finalPacket.UnreadLength)));
                else
                    disconnect = new NetDisconnect((DisconnectCode)disconnectCode);
            }
            else
            {
                disconnect = new NetDisconnect(DisconnectCode.InvalidPacket);
            }

            Disconnect(netEndPoint, disconnect, false);
        }

        private void Reset(bool clearUnmanaged)
        {
            cancellationTokenSource.Cancel();

            if (clearUnmanaged)
            {
                tcpSocket.Shutdown(SocketShutdown.Both);
                udpSocket.Shutdown(SocketShutdown.Both);
                tcpSocket.Close();
                udpSocket.Close();
                cancellationTokenSource.Dispose();
            }

            connections.Clear();
            connectionRequestQueue.Clear();
            connectionResultQueue.Clear();
            disconnectionQueue.Clear();
            beginReceiveQueue.Clear();
            packetReceiveQueue.Clear();
            errorQueue.Clear();
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
                    connectionRequestQueue = null;
                    connectionResultQueue = null;
                    disconnectionQueue = null;
                    beginReceiveQueue = null;
                    packetReceiveQueue = null;
                    errorQueue = null;
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
