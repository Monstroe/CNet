using System;
using System.Buffers.Binary;
using System.Text;

namespace CNet
{
    /// <summary>
    /// Represents a network packet.
    /// </summary>
    public class NetPacket : IDisposable
    {
        internal byte[] ByteArray
        {
            get => buffer;
        }

        public ArraySegment<byte> ByteSegment
        {
            get => new ArraySegment<byte>(buffer, startIndex, Length);
        }

        public TransportProtocol Protocol { get; }

        internal int StartIndex
        {
            get => startIndex;
            set => startIndex = value;
        }

        /// <summary>
        /// Gets the total length of the packet.
        /// </summary>
        public int Length
        {
            get => count - startIndex;
            internal set => count = value + startIndex;
        }

        /// <summary>
        /// Gets the unread length of the packet (relative to CurrentIndex).
        /// </summary>
        /// <seealso cref="CurrentIndex"/>
        public int UnreadLength
        {
            get => count - currentIndex;
        }

        /// <summary>
        /// Gets the current index of the packet. Bytes will be read from this point.
        /// </summary>
        public int CurrentIndex
        {
            get => currentIndex - startIndex;
            set => currentIndex = value >= 0 ? value + startIndex : throw new ArgumentOutOfRangeException(nameof(value), "CurrentIndex must be non-negative.");
        }

        private readonly byte[] buffer;
        private int startIndex;
        private int currentIndex;
        private int count;
        private readonly NetSystem system;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetPacket"/> class.
        /// </summary>
        /// <param name="system">The network system the packet will be sent over.</param>
        /// <param name="protocol">The protocol that will be used to send this packet.</param>
        public NetPacket(NetSystem system, TransportProtocol protocol) : this(system, protocol, sizeof(int))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetPacket"/> class with initial data
        /// </summary>
        /// <param name="system">The network system the packet will be sent over.</param>
        /// <param name="protocol">The protocol that will be used to send this packet.</param>
        /// <param name="initialData">The initial data to be written to the packet.</param>
        public NetPacket(NetSystem system, TransportProtocol protocol, ArraySegment<byte> initialData) : this(system, protocol, sizeof(int))
        {
            SetBytes(initialData);
        }

        // This constructor gives the programmer control over the start index (ONLY USED INTERNALLY)
        internal NetPacket(NetSystem system, TransportProtocol protocol, int startIndex)
        {
            this.buffer = system.PacketPool.Rent((protocol == TransportProtocol.TCP ? system.TCP.MAX_PACKET_SIZE : system.UDP.MAX_PACKET_SIZE) + sizeof(int));
            this.startIndex = startIndex;
            this.Protocol = protocol;
            this.count = startIndex;
            this.currentIndex = startIndex;
            this.system = system;
        }

        /// <summary>
        /// Writes a byte to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(byte value)
        {
            buffer[count++] = value;
        }

        /// <summary>
        /// Writes a byte array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(byte[] value)
        {
            int length = value.Length;
            Write(length);
            Buffer.BlockCopy(value, 0, buffer, count, length);
            count += length;
        }

        /// <summary>
        /// Writes a signed byte to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(sbyte value)
        {
            buffer[count++] = (byte)value;
        }

        /// <summary>
        /// Writes a signed byte array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(sbyte[] value)
        {
            int length = value.Length;
            Write(length);
            Buffer.BlockCopy(value, 0, buffer, count, length);
            count += length;
        }

        /// <summary>
        /// Writes a boolean to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(bool value)
        {
            buffer[count] = (byte)(value ? 1 : 0);
            count += sizeof(bool);
        }

        /// <summary>
        /// Writes a boolean array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(bool[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var b in value)
                Write(b);
        }

        /// <summary>
        /// Writes a character to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(char value)
        {
            Span<byte> encoded = stackalloc byte[4]; // max 4 bytes per UTF-8 code point
            int byteCount = Encoding.UTF8.GetBytes(value.ToString(), encoded);
            buffer[count++] = (byte)byteCount;
            encoded.Slice(0, byteCount).CopyTo(buffer.AsSpan(count));
            count += byteCount;
        }

        /// <summary>
        /// Writes a character array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(char[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var c in value)
                Write(c);
        }

        /// <summary>
        /// Writes a double to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(double value)
        {
            BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(count, sizeof(double)), BitConverter.DoubleToInt64Bits(value));
            count += sizeof(double);
        }

        /// <summary>
        /// Writes a double array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(double[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var d in value)
                Write(d);
        }

        /// <summary>
        /// Writes a float to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(float value)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(count, sizeof(float)), BitConverter.SingleToInt32Bits(value));
            count += sizeof(float);
        }

        /// <summary>
        /// Writes a float array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(float[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var f in value)
                Write(f);
        }

        /// <summary>
        /// Writes an integer to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(int value)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(count, sizeof(int)), value);
            count += sizeof(int);
        }

        /// <summary>
        /// Writes an integer array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(int[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var i in value)
                Write(i);
        }

        /// <summary>
        /// Writes a long to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(long value)
        {
            BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(count, sizeof(long)), value);
            count += sizeof(long);
        }

        /// <summary>
        /// Writes a long array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(long[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var l in value)
                Write(l);
        }

        /// <summary>
        /// Writes a short to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(short value)
        {
            BinaryPrimitives.WriteInt16BigEndian(buffer.AsSpan(count, sizeof(short)), value);
            count += sizeof(short);
        }

        /// <summary>
        /// Writes a short array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(short[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var s in value)
                Write(s);
        }

        /// <summary>
        /// Writes an unsigned integer to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(uint value)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(count, sizeof(uint)), value);
            count += sizeof(uint);
        }

        /// <summary>                                                                                                                                       
        /// Writes an unsigned integer array to the packet.                                                                                                                                                                 
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(uint[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var u in value)
                Write(u);
        }

        /// <summary>
        /// Writes an unsigned long to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(ulong value)
        {
            BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(count, sizeof(ulong)), value);
            count += sizeof(ulong);
        }

        /// <summary>
        /// Writes an unsigned long array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(ulong[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var u in value)
                Write(u);
        }

        /// <summary>
        /// Writes an unsigned short to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(ushort value)
        {
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(count, sizeof(ushort)), value);
            count += sizeof(ushort);
        }

        /// <summary>
        /// Writes an unsigned short array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(ushort[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var u in value)
                Write(u);
        }

        /// <summary>
        /// Writes a string to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(string value)
        {
            int byteCount = Encoding.UTF8.GetByteCount(value);
            Write(byteCount);
            Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, count);
            count += byteCount;
        }

        /// <summary>
        /// Writes a string array to the packet.
        /// </summary>
        /// <param name="value">The value to be written to the packet.</param>
        public void Write(string[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var s in value)
                Write(s);
        }

        /// <summary>
        /// Serializes a network syncable class to the packet.
        /// </summary>
        /// <typeparam name="T">The network syncable class type.</typeparam>
        /// <param name="value">The network syncable class.</param>
        public void SerializeClass<T>(T value) where T : class
        {
            system.Serializer.Write(this, value);
        }

        /// <summary>
        /// Serializes a network syncable struct to the packet.
        /// </summary>
        /// <typeparam name="T">The network syncable struct type.</typeparam>
        /// <param name="value">The network syncable struct.</param>
        public void SerializeStruct<T>(T value) where T : struct
        {
            system.Serializer.Write(this, value);
        }

        /// <summary>
        /// Read a byte from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of a byte.</param>
        /// <returns>The read byte.</returns>
        /// <seealso cref="CurrentIndex"/>
        public byte ReadByte(bool moveIndexPosition = true)
        {
            int typeSize = 1;
            var value = buffer[currentIndex];
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        /// <summary>
        /// Read a byte array from the packet
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the byte array.</param>
        /// <returns>The read byte array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public byte[] ReadBytes(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new byte[length];
            Buffer.BlockCopy(buffer, currentIndex, value, 0, length);
            currentIndex = moveIndexPosition ? currentIndex + length : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read an sbyte from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of an sbyte.</param>
        /// <returns>The read sbyte.</returns>
        /// <seealso cref="CurrentIndex"/>
        public sbyte ReadSByte(bool moveIndexPosition = true)
        {
            int typeSize = 1;
            var value = (sbyte)buffer[currentIndex];
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        /// <summary>
        /// Read an sbyte array from the packet
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the sbyte array.</param>
        /// <returns>The read sbyte array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public sbyte[] ReadSBytes(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new sbyte[length];
            Buffer.BlockCopy(buffer, currentIndex, value, 0, length);
            currentIndex = moveIndexPosition ? currentIndex + length : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read a boolean from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of a boolean.</param>
        /// <returns>The read boolean.</returns>
        /// <seealso cref="CurrentIndex"/>
        public bool ReadBool(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(bool);
            var value = buffer[currentIndex] != 0;
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        /// <summary>
        /// Read a boolean array from the packet
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the boolean array.</param>
        /// <returns>The read boolean array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public bool[] ReadBools(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new bool[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadBool();
            currentIndex = moveIndexPosition ? currentIndex : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read a character from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of a character.</param>
        /// <returns>The read character.</returns>
        /// <seealso cref="CurrentIndex"/>
        public char ReadChar(bool moveIndexPosition = true)
        {
            int byteCount = buffer[currentIndex++];
            var value = Encoding.UTF8.GetString(buffer, currentIndex, byteCount);
            currentIndex += moveIndexPosition ? byteCount : -1;
            return value[0];
        }

        /// <summary>
        /// Read a character array from the packet
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the character array.</param>
        /// <returns>The read character array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public char[] ReadChars(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new char[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadChar();
            currentIndex = moveIndexPosition ? currentIndex : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read a double from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of a double.</param>
        /// <returns>The read double.</returns>
        /// <seealso cref="CurrentIndex"/>
        public double ReadDouble(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(double);
            var value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(GetBytes(typeSize, false)));
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        /// <summary>
        /// Read a double array from the packet
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the double array.</param>
        /// <returns>The read double array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public double[] ReadDoubles(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new double[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadDouble();
            currentIndex = moveIndexPosition ? currentIndex : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read a float from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of a float.</param>
        /// <returns>The read float.</returns>
        /// <seealso cref="CurrentIndex"/>
        public float ReadFloat(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(float);
            var value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(GetBytes(typeSize, false)));
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        /// <summary>
        /// Read a float array from the packet
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the float array.</param>
        /// <returns>The read float array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public float[] ReadFloats(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new float[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadFloat();
            currentIndex = moveIndexPosition ? currentIndex : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read an integer from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of an integer.</param>
        /// <returns>The read integer.</returns>
        /// <seealso cref="CurrentIndex"/>
        public int ReadInt(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(int);
            var value = BinaryPrimitives.ReadInt32BigEndian(GetBytes(typeSize, false));
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        /// <summary>
        /// Read an integer array from the packet
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the integer array.</param>
        /// <returns>The read integer array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public int[] ReadInts(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new int[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadInt();
            currentIndex = moveIndexPosition ? currentIndex : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read a long from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of a long.</param>
        /// <returns>The read long.</returns>
        /// <seealso cref="CurrentIndex"/>
        public long ReadLong(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(long);
            var value = BinaryPrimitives.ReadInt64BigEndian(GetBytes(typeSize, false));
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        /// <summary>
        /// Read a long array from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the long array.</param>
        /// <returns>The read long array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public long[] ReadLongs(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new long[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadLong();
            currentIndex = moveIndexPosition ? currentIndex : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read a short from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of a short.</param>
        /// <returns>The read short.</returns>
        /// <seealso cref="CurrentIndex"/>
        public short ReadShort(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(short);
            var value = BinaryPrimitives.ReadInt16BigEndian(GetBytes(typeSize, false));
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        /// <summary>
        /// Read a short array from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the short array.</param>
        /// <returns>The read short array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public short[] ReadShorts(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new short[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadShort();
            currentIndex = moveIndexPosition ? currentIndex : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read an unsigned integer from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of an unsigned integer.</param>
        /// <returns>The read unsigned integer.</returns>
        /// <seealso cref="CurrentIndex"/>
        public uint ReadUInt(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(uint);
            var value = BinaryPrimitives.ReadUInt32BigEndian(GetBytes(typeSize, false));
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        /// <summary>
        /// Read an unsigned integer array from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the unsigned integer array.</param>
        /// <returns>The read unsigned integer array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public uint[] ReadUInts(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new uint[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadUInt();
            currentIndex = moveIndexPosition ? currentIndex : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read an unsigned long from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of an unsigned long.</param>
        /// <returns>The read unsigned long.</returns>
        /// <seealso cref="CurrentIndex"/>
        public ulong ReadULong(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(ulong);
            var value = BinaryPrimitives.ReadUInt64BigEndian(GetBytes(typeSize, false));
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        /// <summary>
        /// Read an unsigned long array from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the unsigned long array.</param>
        /// <returns>The read unsigned long array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public ulong[] ReadULongs(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new ulong[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadULong();
            currentIndex = moveIndexPosition ? currentIndex : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read an unsigned short from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of an unsigned short.</param>
        /// <returns>The read unsigned short.</returns>
        /// <seealso cref="CurrentIndex"/>
        public ushort ReadUShort(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(short);
            var value = BinaryPrimitives.ReadUInt16BigEndian(GetBytes(typeSize, false));
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        /// <summary>
        /// Read an unsigned short array from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the unsigned short array.</param>
        /// <returns>The read unsigned short array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public ushort[] ReadUShorts(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new ushort[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadUShort();
            currentIndex = moveIndexPosition ? currentIndex : cachedIndex;
            return value;
        }

        /// <summary>
        /// Read a string from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the string.</param>
        /// <returns>The read string.</returns>
        /// <seealso cref="CurrentIndex"/>
        public string ReadString(bool moveIndexPosition = true)
        {
            int strBytes = ReadInt();
            var value = Encoding.UTF8.GetString(GetBytes(strBytes, false));
            currentIndex += moveIndexPosition ? strBytes : -sizeof(int);
            return value;
        }

        /// <summary>
        /// Read a string array from the packet.
        /// </summary>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the string array.</param>
        /// <returns>The read string array.</returns>
        /// <seealso cref="CurrentIndex"/>
        public string[] ReadStrings(bool moveIndexPosition = true)
        {
            int cachedIndex = currentIndex;
            int length = ReadInt();
            var value = new string[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadString();
            currentIndex = moveIndexPosition ? currentIndex : cachedIndex;
            return value;
        }

        /// <summary>
        /// Deserializes a network syncable class from the packet.
        /// </summary>
        /// <typeparam name="T">The network syncable class type.</typeparam>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the network syncable class.</param>
        /// <returns>The deserialized network syncable class.</returns>
        /// <seealso cref="CurrentIndex"/>
        public T DeserializeClass<T>(bool moveIndexPosition = true) where T : class, new()
        {
            int tempIndex = currentIndex;
            T obj = system.Serializer.Read<T>(this);
            currentIndex = moveIndexPosition ? currentIndex : tempIndex;
            return obj;
        }

        /// <summary>
        /// Deserializes a network syncable struct from the packet.
        /// </summary>
        /// <typeparam name="T">The network syncable struct type.</typeparam>
        /// <param name="moveIndexPosition">If true, will increment CurrentIndex by the size of the network syncable struct.</param>
        /// <returns>The deserialized network syncable struct.</returns>
        /// <seealso cref="CurrentIndex"/>
        public T DeserializeStruct<T>(bool moveIndexPosition = true) where T : struct
        {
            int tempIndex = currentIndex;
            T obj = system.Serializer.Read<T>(this);
            currentIndex = moveIndexPosition ? currentIndex : tempIndex;
            return obj;
        }

        // This method will write the length of the packet sizeof(int) bytes before the start index (ONLY USED INTERNALLY)
        internal void SetLength()
        {
            int length = count - sizeof(int);
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(startIndex - sizeof(int), sizeof(int)), length);
        }

        // This method will write a byte array to the stream without adding its length beforehand (ONLY USED INTERNALLY)
        internal void SetBytes(ArraySegment<byte> value)
        {
            Buffer.BlockCopy(value.Array, value.Offset, buffer, count, value.Count);
            count += value.Count;
        }

        // This method will read a specific amount of bytes from the buffer instead of getting the length from the byte stream (ONLY USED INTERNALLY)
        internal ArraySegment<byte> GetBytes(int length, bool moveIndexPosition = true)
        {
            int typeSize = length;
            ArraySegment<byte> segment = new ArraySegment<byte>(buffer, currentIndex, length);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return segment;
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
                    system.PacketPool.Return(buffer);
                    startIndex = 0;
                    currentIndex = 0;
                    count = 0;
                }

                disposed = true;
            }
        }

        ~NetPacket()
        {
            Dispose(false);
        }
    }
}
