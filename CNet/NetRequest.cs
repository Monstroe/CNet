using System;
using System.Net;

namespace CNet
{
    public class NetRequest
    {
        public IPEndPoint ClientEndPoint
        {
            get { return clientEP.TCPEndPoint; }
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
                throw new InvalidOperationException("Accept() or Deny() has already been called.");
            }

            clientAccepted = true;
            netSystem.HandleConnectionResultOnMainThread(true, clientEP);
            return clientEP;
        }

        public void Deny()
        {
            if (clientAccepted || clientDenied)
            {
                throw new InvalidOperationException("Accept() or Deny() has already been called.");
            }

            clientDenied = true;
            netSystem.HandleConnectionResultOnMainThread(false, clientEP);
        }
    }
}
