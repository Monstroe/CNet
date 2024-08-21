﻿using System;
using System.Collections.Generic;

namespace CNet
{
    public class NetListener
    {
        private NetSystem system;

        public NetSystem System { get { return system; } }
        public ProtocolSettings TCP { get { return system.TCP; } }
        public ProtocolSettings UDP { get { return system.UDP; } }

        public string Address
        {
            get { return system.Address; }
            set { system.Address = value; }
        }

        public int Port
        {
            get { return system.Port; }
            set { system.Port = value; }
        }

        public int MaxPendingConnections { get { return system.MaxPendingConnections; } }

        public List<NetEndPoint> RemoteEndPoints { get { return system.RemoteEndPoints; } }

        public NetListener()
        {
            system = new NetSystem();
        }

        public NetListener(int port)
        {
            system = new NetSystem(port);
        }

        public void RegisterInterface(IEventNetListener iListener)
        {
            system.RegisterInterface(iListener);
        }

        public void Update()
        {
            system.Update();
        }

        public void Listen()
        {
            system.Listen();
        }

        public void Send(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol)
        {
            system.Send(remoteEP, packet, protocol);
        }

        public void Disconnect(NetEndPoint remoteEP)
        {
            system.Disconnect(remoteEP);
        }

        public void Disconnect(NetEndPoint remoteEP, NetPacket disconnectPacket)
        {
            system.Disconnect(remoteEP, disconnectPacket);
        }

        public void DisconnectForcefully(NetEndPoint remoteEP)
        {
            system.DisconnectForcefully(remoteEP);
        }

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
