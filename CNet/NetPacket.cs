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

        public int Length
        {
            get => count - startIndex;
            internal set => count = value + startIndex;
        }

        public int UnreadLength
        {
            get => count - currentIndex;
        }

        public int CurrentIndex
        {
            get => currentIndex - startIndex;
            set => currentIndex = value + startIndex;
        }

        private readonly byte[] buffer;
        private int startIndex;
        private int currentIndex;
        private int count;

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

        public void Write(byte value)
        {
            buffer[count++] = value;
        }

        public void Write(byte[] value)
        {
            int length = value.Length;
            Write(length);
            Buffer.BlockCopy(value, 0, buffer, count, length);
            count += length;
        }

        public void Write(sbyte value)
        {
            buffer[count++] = (byte)value;
        }

        public void Write(sbyte[] value)
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

        public void Write(bool value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, count, sizeof(bool));
            count += sizeof(bool);
        }

        public void Write(bool[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var b in value)
                Write(b);
        }

        public void Write(char value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, count, sizeof(char));
            count += sizeof(char);
        }

        public void Write(char[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var c in value)
                Write(c);
        }

        public void Write(double value)
        {
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(double));
            count += sizeof(double);
        }

        public void Write(double[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var d in value)
                Write(d);
        }

        public void Write(float value)
        {
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(float));
            count += sizeof(float);
        }

        public void Write(float[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var f in value)
                Write(f);
        }

        public void Write(int value)
        {
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(int));
            count += sizeof(int);
        }

        public void Write(int[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var i in value)
                Write(i);
        }

        public void Write(long value)
        {
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(long));
            count += sizeof(long);
        }

        public void Write(long[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var l in value)
                Write(l);
        }

        public void Write(short value)
        {
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(short));
            count += sizeof(short);
        }

        public void Write(short[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var s in value)
                Write(s);
        }

        public void Write(uint value)
        {
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(uint));
            count += sizeof(uint);
        }

        public void Write(uint[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var u in value)
                Write(u);
        }

        public void Write(ulong value)
        {
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(ulong));
            count += sizeof(ulong);
        }

        public void Write(ulong[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var u in value)
                Write(u);
        }

        public void Write(ushort value)
        {
            Buffer.BlockCopy(ToProperEndian(BitConverter.GetBytes(value)), 0, buffer, count, sizeof(ushort));
            count += sizeof(ushort);
        }

        public void Write(ushort[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var u in value)
                Write(u);
        }

        public void Write(string value)
        {
            Write((uint)value.Length);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(value), 0, buffer, count, value.Length);
            count += value.Length;
        }

        public void Write(string[] value)
        {
            int length = value.Length;
            Write(length);
            foreach (var s in value)
                Write(s);
        }

        public void SerializeClass<T>(T value) where T : class
        {
            SerializeManager.Instance.Write(this, value);
        }

        public void SerializeStruct<T>(T value) where T : struct
        {
            SerializeManager.Instance.Write(this, value);
        }

        public byte ReadByte(bool moveIndexPosition = true)
        {
            int typeSize = 1;
            var value = buffer[currentIndex];
            currentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

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
