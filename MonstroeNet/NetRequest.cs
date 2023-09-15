using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MonstroeNet
{
    public class NetRequest
    {
        public IPEndPoint ClientEndPoint
        {
            get
            {
                return clientEP.EndPoint;
            }
        }

        internal NetEndPoint clientEP;
        internal NetSystem netSystem;

        private bool clientAccepted;
        private bool clientDenied;
        
        internal NetRequest(NetEndPoint requestingEP, NetSystem netSystem)
        {
            clientEP = requestingEP;
            this.netSystem = netSystem;

            clientAccepted = false;
            clientDenied = false;
        }

        public NetEndPoint Accept()
        {
            if(clientAccepted || clientDenied)
            {
                // Throw already called error
                throw new InvalidOperationException();
            }

            clientAccepted = true;
            netSystem.connectionResultQueue.Enqueue((true, clientEP));
            return clientEP;
        }

        public void Deny()
        {
            if (clientAccepted || clientDenied)
            {
                // Throw already called error
                throw new InvalidOperationException();
            }

            clientDenied = true;
            netSystem.connectionResultQueue.Enqueue((false, clientEP));

            /*if (!clientDenied && !clientAccepted)
            {
                clientEndPoint.tcpSocket?.Close();
            }
            clientDenied = true;*/
        }
    }
}
