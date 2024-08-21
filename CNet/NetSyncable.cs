using System;
using System.Reflection;

namespace CNet
{
    /// <summary>
    /// Represents an attribute that indicates a class or struct is syncable over the network.
    /// </summary>
    /// <remarks>
    /// This attribute can be applied to classes or structs to indicate that they can be synchronized over the network.
    /// </remarks>
    public class NetSyncableAttribute : Attribute
    {
        /// <summary>
        /// Gets the binding flags used for synchronization.
        /// </summary>
        public BindingFlags BindingFlags { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetSyncableAttribute"/> class with default binding flags (public instance members).
        /// </summary>
        public NetSyncableAttribute()
        {
            BindingFlags = BindingFlags.Public | BindingFlags.Instance;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetSyncableAttribute"/> class with the specified binding flags.
        /// </summary>
        /// <param name="bindingAttr">The binding flags used for synchronization.</param>
        public NetSyncableAttribute(BindingFlags bindingAttr)
        {
            BindingFlags = bindingAttr;
        }
    }
}