using System;
using System.Net;

namespace CNet
{
    /// <summary>
    /// Represents a network connection request made to a server.
    /// </summary>
    public class NetRequest
    {
        /// <summary>
        /// Gets the client's endpoint.
        /// </summary>
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

        /// <summary>
        /// Accepts the network request and establishes a connection with the client.
        /// </summary>
        /// <returns>The client's IP endpoint.</returns>
        /// <exception cref="InvalidOperationException">Thrown if Accept() or Deny() has already been called.</exception>
        public NetEndPoint Accept()
        {
            if (clientAccepted || clientDenied)
            {
                throw new InvalidOperationException("Accept() or Deny() has already been called.");
            }

            clientAccepted = true;
            netSystem.HandleConnectionResult(true, clientEP);
            return clientEP;
        }

        /// <summary>
        /// Denies the network request and rejects the connection with the client.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if Accept() or Deny() has already been called.</exception>
        public void Deny()
        {
            if (clientAccepted || clientDenied)
            {
                throw new InvalidOperationException("Accept() or Deny() has already been called.");
            }

            clientDenied = true;
            netSystem.HandleConnectionResult(false, clientEP);
        }
    }
}