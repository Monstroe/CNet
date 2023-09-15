using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace MonstroeNet
{
    public class NetDisconnect
    {
        public DisconnectCode DisconnectCode
        {
            get { return disconnectCode; }
        }

        public NetPacket DisconnectData
        {
            get { return disconnectData; }
        }

        public SocketError SocketError
        {
            get { return socketError; }
        }

        private DisconnectCode disconnectCode;
        private NetPacket disconnectData;
        private SocketError socketError;

        internal NetDisconnect(DisconnectCode reason)
        {
            disconnectCode = reason;
        }

        internal NetDisconnect(DisconnectCode reason, NetPacket data) : this(reason)
        {
            disconnectData = data;
        }

        internal NetDisconnect(DisconnectCode reason, SocketError error) : this(reason)
        {
            socketError = error;
        }

        internal NetDisconnect(DisconnectCode reason, NetPacket data, SocketError error) : this(reason, data)
        {
            socketError = error;
        }
    }
}
