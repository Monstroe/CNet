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
        public NetEndPoint ConnectingEP { get; private set; }

        private NetSystem system;

        private bool clientAccepted;
        private bool clientDenied;

        internal NetRequest(NetEndPoint connectingEP, NetSystem system)
        {
            ConnectingEP = connectingEP;
            this.system = system;

            clientAccepted = false;
            clientDenied = false;
        }

        /// <summary>
        /// Accepts the network request and establishes a connection with the client.
        /// </summary>
        /// <returns>The client's IP endpoint.</returns>
        /// <exception cref="InvalidOperationException">Thrown if Accept() or Deny() has already been called.</exception>
        public void AcceptIfKey(string connectionKey)
        {
            if (clientAccepted || clientDenied)
            {
                throw new InvalidOperationException("Accept() or Deny() has already been called.");
            }

            clientAccepted = true;
            system.HandleConnectionResult(true, ConnectingEP, connectionKey);
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
            system.HandleConnectionResult(false, ConnectingEP, null);
        }
    }
}