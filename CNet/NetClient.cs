using System;

namespace CNet
{
    /// <summary>
    /// Represents a network client.
    /// </summary>
    public class NetClient
    {
        private NetSystem system;

        /// <summary>
        /// Gets the underlying NetSystem.
        /// </summary>
        public NetSystem System { get { return system; } }

        /// <summary>
        /// Gets the settings for the TCP protocol.
        /// </summary>
        public ProtocolSettings TCP { get { return system.TCP; } }
        /// <summary>
        /// Gets the settings for the UDP protocol.
        /// </summary>
        public ProtocolSettings UDP { get { return system.UDP; } }

        /// <summary>
        /// Gets the address of the server.
        /// </summary>
        public string Address
        {
            get { return system.Address; }
            set { system.Address = value; }
        }

        /// <summary>
        /// Gets the port of the server.
        /// </summary>
        public int Port
        {
            get { return system.Port; }
            set { system.Port = value; }
        }

        /// <summary>
        /// Gets a value indicating whether the client is connected.
        /// </summary>
        public bool IsConnected { get { return system.IsConnected; } }

        /// <summary>
        /// Gets the remote end point.
        /// </summary>
        public NetEndPoint RemoteEndPoint { get { return system.RemoteEndPoint; } }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetClient"/> class.
        /// </summary>
        public NetClient()
        {
            system = new NetSystem();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetClient"/> class.
        /// </summary>
        /// <param name="address">The address of the server.</param>
        /// <param name="port">The port of the server.</param>
        public NetClient(string address, int port)
        {
            system = new NetSystem(address, port);
        }

        /// <summary>
        /// Registers an interface to receive network events.
        /// </summary>
        /// <param name="iClient">The client interface to register.</param>
        public void RegisterInterface(IEventNetClient iClient)
        {
            system.RegisterInterface(iClient);
        }

        /// <summary>
        /// Polls all pending events.
        /// </summary>
        /// <remarks>
        /// This method should be called in an update loop. It will receive all pending events on the main thread.
        /// </remarks>
        public void Update()
        {
            system.Update();
        }

        /// <summary>
        /// Connects to the server.
        /// </summary>
        public void Connect()
        {
            system.Connect();
        }

        /// <summary>
        /// Sends a network packet using the specified protocol.
        /// </summary>
        /// <param name="remoteEP">The remote end point to send the packet to.</param>
        /// <param name="packet">The network packet to send.</param>
        /// <param name="protocol">The protocol to use for sending the packet.</param>
        public void Send(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol)
        {
            system.Send(remoteEP, packet, protocol);
        }

        /// <summary>
        /// Disconnects from the network.
        /// </summary>
        public void Disconnect()
        {
            system.Disconnect(RemoteEndPoint);
        }

        /// <summary>
        /// Disconnects from the network with a specified disconnect packet.
        /// </summary>
        /// <param name="disconnectPacket">The disconnect packet to send.</param>
        public void Disconnect(NetPacket disconnectPacket)
        {
            system.Disconnect(RemoteEndPoint, disconnectPacket);
        }

        /// <summary>
        /// Disconnects from the network forcefully.
        /// </summary>
        public void DisconnectForcefully()
        {
            system.DisconnectForcefully(RemoteEndPoint);
        }

        /// <summary>
        /// Closes the client.
        /// </summary>
        /// /// <param name="sendDisconnectPacketToRemote">Whether to send a disconnect packet to the remote end point.</param>
        public void Close(bool sendDisconnectPacketToRemote) 
        {
            system.Close(sendDisconnectPacketToRemote);
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
                    system.Dispose();
                }

                disposed = true;
            }
        }

        ~NetClient()
        {
            Dispose(false);
        }
    }
}
