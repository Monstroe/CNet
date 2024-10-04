using System;
using System.Buffers;
using System.Text;

namespace CNet
{
    public class NetPacket : IDisposable
    {
        internal byte[] ByteArray
        {
            get => buffer;
        }

        internal ArraySegment<byte> ByteSegment
        {
            get => new ArraySegment<byte>(buffer, startIndex, count);
        }

        internal PacketProtocol Protocol { get; }

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
            set => currentIndex = value + startIndex;
        }

        private readonly byte[] buffer;
        private int startIndex;
        private int currentIndex;
        private int count;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetPacket"/> class.
        /// </summary>
        /// <param name="system">The network system the packet will be sent over.</param>
        /// <param name="protocol">The protocol that will be used to send this packet.</param>
        public NetPacket(NetSystem system, PacketProtocol protocol) : this(system, protocol, sizeof(int)) { }

        internal NetPacket(NetSystem system, PacketProtocol protocol, int startIndex)
        {
            this.buffer = ArrayPool<byte>.Shared.Rent((protocol == PacketProtocol.TCP ? system.TCP.MaxPacketSize : system.UDP.MaxPacketSize) + sizeof(int));
            this.startIndex = startIndex;
            this.Protocol = protocol;
            this.count = startIndex;
            this.currentIndex = startIndex;
        }

        internal NetPacket(byte[] buffer, PacketProtocol protocol)
        {
            this.buffer = buffer;
            this.startIndex = 0;
            this.Protocol = protocol;
            this.count = 0;
            this.currentIndex = 0;
        }

        internal void InsertLength()
        {
            InsertLength(0);
        }

        // WARNING: This method will overwrite the first 4 bytes of the buffer (after the startIndex default offset)
        internal void InsertLength(int offset)
        {
            int length = count - sizeof(int) - offset;
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(length)), 0, buffer, startIndex - sizeof(int) + offset, sizeof(int));
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

        // This method will write a byte array to the stream without adding its length beforehand (ONLY USED INTERNALLY)
        internal void WriteInternal(byte[] value)
        {
            Buffer.BlockCopy(value, 0, buffer, count, value.Length);
            count += value.Length;
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
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, count, sizeof(bool));
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
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, count, sizeof(char));
            count += sizeof(char);
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
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(double));
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
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(float));
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
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(int));
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
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(long));
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
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(short));
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
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(uint));
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
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(ulong));
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
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(ushort));
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
            Write((uint)value.Length);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(value), 0, buffer, count, value.Length);
            count += value.Length;
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
            SerializeManager.Instance.Write(this, value);
        }

        /// <summary>
        /// Serializes a network syncable struct to the packet.
        /// </summary>
        /// <typeparam name="T">The network syncable struct type.</typeparam>
        /// <param name="value">The network syncable struct.</param>
        public void SerializeStruct<T>(T value) where T : struct
        {
            SerializeManager.Instance.Write(this, value);
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
            int length = ReadInt();
            int typeSize = length + sizeof(int);
            var value = new byte[length];
            Buffer.BlockCopy(buffer, currentIndex, value, 0, length);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        // This method will read a specific amount of bytes from the buffer instead of getting the length from the byte stream (ONLY USED INTERNALLY)
        internal byte[] ReadBytesInternal(int length, bool moveIndexPosition = true)
        {
            int typeSize = length;
            var value = new byte[length];
            Buffer.BlockCopy(buffer, currentIndex, value, 0, length);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public sbyte ReadSByte(bool moveIndexPosition = true)
        {
            int typeSize = 1;
            var value = (sbyte)buffer[currentIndex];
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public sbyte[] ReadSBytes(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length + sizeof(int);
            var value = new sbyte[length];
            Buffer.BlockCopy(buffer, currentIndex, value, 0, length);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public bool ReadBool(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(bool);
            var value = BitConverter.ToBoolean(new byte[] { buffer[currentIndex] }, 0);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public bool[] ReadBools(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length * sizeof(bool) + sizeof(int);
            var value = new bool[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadBool();
            currentIndex -= moveIndexPosition ? 0 : typeSize;
            return value;
        }

        public char ReadChar(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(char);
            var value = BitConverter.ToChar(new byte[] { buffer[currentIndex] }, 0);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public char[] ReadChars(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length * sizeof(char) + sizeof(int);
            var value = new char[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadChar();
            currentIndex -= moveIndexPosition ? 0 : typeSize;
            return value;
        }

        public double ReadDouble(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(double);
            var value = BitConverter.ToDouble(ToProperEndian(ReadBytesInternal(typeSize, false)), 0);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public double[] ReadDoubles(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length * sizeof(double) + sizeof(int);
            var value = new double[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadDouble();
            currentIndex -= moveIndexPosition ? 0 : typeSize;
            return value;
        }

        public float ReadFloat(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(float);
            var value = BitConverter.ToSingle(ToProperEndian(ReadBytesInternal(typeSize, false)), 0);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public float[] ReadFloats(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length * sizeof(float) + sizeof(int);
            var value = new float[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadFloat();
            currentIndex -= moveIndexPosition ? 0 : typeSize;
            return value;
        }

        public int ReadInt(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(int);
            var value = BitConverter.ToInt32(ToProperEndian(ReadBytesInternal(typeSize, false)), 0);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public int[] ReadInts(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length * sizeof(int) + sizeof(int);
            var value = new int[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadInt();
            currentIndex -= moveIndexPosition ? 0 : typeSize;
            return value;
        }

        public long ReadLong(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(long);
            var value = BitConverter.ToInt64(ToProperEndian(ReadBytesInternal(typeSize, false)), 0);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public long[] ReadLongs(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length * sizeof(long) + sizeof(int);
            var value = new long[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadLong();
            currentIndex -= moveIndexPosition ? 0 : typeSize;
            return value;
        }

        public short ReadShort(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(short);
            var value = BitConverter.ToInt16(ToProperEndian(ReadBytesInternal(typeSize, false)), 0);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public short[] ReadShorts(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length * sizeof(short) + sizeof(int);
            var value = new short[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadShort();
            currentIndex -= moveIndexPosition ? 0 : typeSize;
            return value;
        }

        public uint ReadUInt(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(uint);
            var value = BitConverter.ToUInt32(ToProperEndian(ReadBytesInternal(typeSize, false)), 0);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public uint[] ReadUInts(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length * sizeof(uint) + sizeof(int);
            var value = new uint[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadUInt();
            currentIndex -= moveIndexPosition ? 0 : typeSize;
            return value;
        }

        public ulong ReadULong(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(ulong);
            var value = BitConverter.ToUInt64(ToProperEndian(ReadBytesInternal(typeSize, false)), 0);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public ulong[] ReadULongs(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length * sizeof(ulong) + sizeof(int);
            var value = new ulong[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadULong();
            currentIndex -= moveIndexPosition ? 0 : typeSize;
            return value;
        }

        public ushort ReadUShort(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(short);
            var value = BitConverter.ToUInt16(ToProperEndian(ReadBytesInternal(typeSize, false)), 0);
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public ushort[] ReadUShorts(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length * sizeof(short) + sizeof(int);
            var value = new ushort[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadUShort();
            currentIndex -= moveIndexPosition ? 0 : typeSize;
            return value;
        }

        public string ReadString(bool moveIndexPosition = true)
        {
            int strLen = ReadInt();
            var value = Encoding.UTF8.GetString(ReadBytesInternal(strLen, false));
            currentIndex += moveIndexPosition ? strLen : -sizeof(int);
            return value;
        }

        public string[] ReadStrings(bool moveIndexPosition = true)
        {
            int length = ReadInt();
            int typeSize = length * sizeof(int) + sizeof(int);
            var value = new string[length];
            for (int i = 0; i < length; i++)
                value[i] = ReadString();
            currentIndex -= moveIndexPosition ? 0 : typeSize;
            return value;
        }

        public T DeserializeClass<T>(bool moveIndexPosition = true) where T : class, new()
        {
            int tempIndex = currentIndex;
            T obj = SerializeManager.Instance.Read<T>(this);
            currentIndex = moveIndexPosition ? currentIndex : tempIndex;
            return obj;
        }

        public T DeserializeStruct<T>(bool moveIndexPosition = true) where T : struct
        {
            int tempIndex = currentIndex;
            T obj = SerializeManager.Instance.Read<T>(this);
            currentIndex = moveIndexPosition ? currentIndex : tempIndex;
            return obj;
        }

        private byte[] ToProperEndian(byte[] value)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(value);
            }
            return value;
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
                    currentIndex = 0;
                    startIndex = 0;
                    count = 0;
                    ArrayPool<byte>.Shared.Return(buffer);
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
