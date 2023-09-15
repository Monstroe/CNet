using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace MonstroeNet
{
    public class NetDisconnect
    {
        public DisconnectReason DisconnectReason
        {
            get { return disconnectReason; }
        }

        public NetPacket DisconnectData
        {
            get { return disconnectData; }
        }

        public SocketError SocketError
        {
            get { return socketError; }
        }

        private DisconnectReason disconnectReason;
        private NetPacket disconnectData;
        private SocketError socketError;

        internal NetDisconnect(DisconnectReason reason)
        {
            disconnectReason = reason;
        }

        internal NetDisconnect(DisconnectReason reason, NetPacket data) : this(reason)
        {
            disconnectData = data;
        }

        internal NetDisconnect(DisconnectReason reason, SocketError error) : this(reason)
        {
            socketError = error;
        }

        internal NetDisconnect(DisconnectReason reason, NetPacket data, SocketError error) : this(reason, data)
        {
            socketError = error;
        }
    }
}
