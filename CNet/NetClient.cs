using System;

namespace CNet
{
    /// <summary>
    /// Represents a network client.
    /// </summary>
    public class NetClient : IDisposable
    {
        private readonly NetSystem system;

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
        /// Gets the miscellaneous connection settings.
        /// </summary>
        public ConnectionSettings ConnectionSettings { get { return system.ConnectionSettings; } }

        /// <summary>
        /// Gets the address of the server.
        /// </summary>
        public string? Address
        {
            get { return system.Address; }
        }

        /// <summary>
        /// Gets the port of the server.
        /// </summary>
        public int Port
        {
            get { return system.Port; }
        }

        /// <summary>
        /// Gets a value indicating whether the client is connected.
        /// </summary>
        public bool IsConnected { get { return system.IsConnected; } }

        /// <summary>
        /// Gets the local end point.
        /// </summary>
        public NetEndPoint? LocalEndPoint { get { return system.LocalEndPoint; } }

        /// <summary>
        /// Gets the remote end point.
        /// </summary>
        public NetEndPoint? RemoteEndPoint { get { return system.RemoteEndPoint; } }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetClient"/> class.
        /// </summary>
        public NetClient()
        {
            system = new NetSystem();
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
        /// <param name="address">The address of the server.</param>
        /// <param name="port">The port of the server.</param>
        /// <param name="connectionKey">The connection key to use.</param>
        public void Connect(string address, int port, string connectionKey)
        {
            system.Connect(address, port, connectionKey);
        }

        /// <summary>
        /// Sends a network packet using the specified protocol.
        /// </summary>
        /// <param name="packet">The network packet to send.</param>
        /// <param name="protocol">The protocol to use for sending the packet.</param>
        public void Send(NetPacket packet, TransportProtocol protocol)
        {
            system.Send(RemoteEndPoint!, packet, protocol);
        }

        /// <summary>
        /// Disconnects from the network with a specified disconnect packet.
        /// </summary>
        /// <param name="disconnectPacket">The disconnect packet to send.</param>
        public void Disconnect(NetPacket? disconnectPacket = null)
        {
            system.Disconnect(RemoteEndPoint!, disconnectPacket);
        }

        /// <summary>
        /// Disconnects from the network forcefully.
        /// </summary>
        public void DisconnectForcefully()
        {
            system.DisconnectForcefully(RemoteEndPoint!);
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
