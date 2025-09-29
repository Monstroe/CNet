using System;

namespace CNet
{
    /// <summary>
    /// Represents a network connection event.
    /// </summary>
    internal class NetConnect
    {

        /// <summary>
        /// Gets the endpoint being connected to.
        /// </summary>
        internal NetEndPoint ConnectingEP { get; }

        /// <summary>
        /// Gets the expiry time of the connection token.
        /// </summary>
        internal DateTime ConnectionTokenExpiry { get; }

        internal NetConnect(NetEndPoint connectingEP, DateTime tokenExpiry)
        {
            ConnectingEP = connectingEP;
            ConnectionTokenExpiry = tokenExpiry;
        }
    }
}
