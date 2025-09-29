using System;
using System.Collections.Generic;

namespace CNet
{
    /// <summary>
    /// Represents a network listener.
    /// </summary>
    public class NetListener : IDisposable
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
        /// Gets or sets the maximum number of pending connections that can be queued.
        /// </summary>
        public int MaxPendingConnections { get { return system.MaxPendingConnections; } set { system.MaxPendingConnections = value; } }

        /// <summary>
        /// Gets the serialize manager for handling serialization and deserialization of objects.
        /// </summary>
        public SerializeManager Serializer { get { return system.Serializer; } }

        /// <summary>
        /// Gets the port of the listener.
        /// </summary>
        public int Port
        {
            get { return system.Port; }
        }

        /// <summary>
        /// Gets the local end point.
        /// </summary>
        public NetEndPoint? LocalEndPoint { get { return system.LocalEndPoint; } }

        /// <summary>
        /// Gets the remote end points.
        /// </summary>
        public List<NetEndPoint>? RemoteEndPoints { get { return system.RemoteEndPoints; } }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetListener"/> class.
        /// </summary>
        public NetListener()
        {
            system = new NetSystem();
        }

        /// <summary>
        /// Registers an interface to receive network events.
        /// </summary>
        /// <param name="iListener">The listener interface to register.</param>
        public void RegisterInterface(IEventNetListener iListener)
        {
            system.RegisterInterface(iListener);
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
        /// Listens for incoming connections.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        public void Listen(int port)
        {
            system.Listen(port);
        }

        /// <summary>
        /// Sends a network packet using the specified protocol.
        /// </summary>
        /// <param name="remoteEP">The remote end point to send the packet to.</param>
        /// <param name="packet">The network packet to send.</param>
        /// <param name="protocol">The protocol to use for sending the packet.</param>
        public void Send(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol)
        {
            system.Send(remoteEP, packet, protocol);
        }

        /// <summary>
        /// Disconnects from the network with a specified disconnect packet.
        /// </summary>
        /// <param name="remoteEP">The remote end point to disconnect from.</param>
        /// <param name="disconnectPacket">The disconnect packet to send.</param>
        public void Disconnect(NetEndPoint remoteEP, NetPacket? disconnectPacket = null)
        {
            system.Disconnect(remoteEP, disconnectPacket);
        }

        /// <summary>
        /// Disconnects from the network forcefully.
        /// </summary>
        /// <param name="remoteEP">The remote end point to disconnect from.</param>
        public void DisconnectForcefully(NetEndPoint remoteEP)
        {
            system.DisconnectForcefully(remoteEP);
        }

        /// <summary>
        /// Closes the listener.
        /// </summary>
        /// /// <param name="sendDisconnectPacketToRemote">Whether to send a disconnect packet to the remote end point(s).</param>
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

        ~NetListener()
        {
            Dispose(false);
        }
    }
}
