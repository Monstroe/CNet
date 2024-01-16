using System;
using System.Collections.Generic;
using System.Text;

namespace MonstroeNet
{
    public class NetClient
    {
        private NetSystem system;

        public NetSystem System { get { return system; } }
        public Protocol TCP { get { return system.TCP; } }
        public Protocol UDP { get { return system.UDP; } }

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

        public bool IsConnected { get { return system.IsConnected; } }

        public NetEndPoint RemoteTCPEndPoint { get { return system.RemoteTCPEndPoint; } }
        public NetEndPoint RemoteUDPEndPoint { get { return system.RemoteUDPEndPoint; } }

        public NetClient()
        {
            system = new NetSystem();
        }

        public NetClient(string address, int port)
        {
            system = new NetSystem(address, port);
        }

        public void RegisterInterface(IEventNetClient iClient)
        {
            system.RegisterInterface(iClient);
        }

        public void Update()
        {
            system.Update();
        }

        public void Connect()
        {
            system.Connect();
        }

        public void Send(NetEndPoint remoteEP, NetPacket packet, PacketProtocol protocol)
        {
            system.Send(remoteEP, packet, protocol);
        }

        public void Disconnect()
        {
            system.Disconnect(RemoteTCPEndPoint);
        }

        public void Disconnect(NetPacket disconnectPacket)
        {
            system.Disconnect(RemoteTCPEndPoint, disconnectPacket);
        }

        public void DisconnectForcefully()
        {
            system.DisconnectForcefully(RemoteTCPEndPoint);
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

        ~NetClient()
        {
            Dispose(false);
        }
    }
}
