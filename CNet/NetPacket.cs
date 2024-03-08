using System;
using System.Collections.Generic;
using System.Text;

namespace CNet
{
    public class NetPacket : IDisposable
    {
        public byte[] ByteArray
        {
            get
            {
                return byteList.ToArray();
            }
        }

        public int Length
        {
            get
            {
                return byteList.Count;
            }
        }

        public int UnreadLength
        {
            get
            {
                return Length - CurrentIndex;
            }
        }

        public int CurrentIndex { get; private set; }

        private List<byte> byteList;

        public NetPacket()
        {
            CurrentIndex = 0;
            byteList = new List<byte>();
        }

        public NetPacket(byte[] data) : this()
        {
            Write(data);
        }

        public void Clear()
        {
            byteList.Clear();
            CurrentIndex = 0;
        }

        public void Remove(int offset, int count)
        {
            byteList.RemoveRange(offset, count);
            CurrentIndex = CurrentIndex > offset ? CurrentIndex - count : CurrentIndex;
        }

        internal void InsertLength()
        {
            InsertLength(0);
        }

        internal void InsertLength(int offset)
        {
            int length = byteList.Count - offset;
            byteList.InsertRange(offset, ToProperEndian(BitConverter.GetBytes(length)));
        }

        public void Write(byte value) { byteList.Add(value); }
        public void Write(byte[] value) { byteList.AddRange(value); }
        public void Write(bool value) { byteList.AddRange(BitConverter.GetBytes(value)); }
        public void Write(char value) { byteList.AddRange(BitConverter.GetBytes(value)); }
        public void Write(double value) { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(float value) { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(int value) { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(long value) { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(short value) { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(uint value) { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(ulong value) { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(ushort value) { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(string value)
        {
            Write(value.Length);
            byteList.AddRange(Encoding.UTF8.GetBytes(value));
        }

        private byte[] ToProperEndian(byte[] value)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(value);
            }
            return value;
        }

        public byte ReadByte(bool moveIndexPosition = true)
        {
            int typeSize = 1;
            var value = byteList[CurrentIndex];
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public byte[] ReadBytes(int length, bool moveIndexPosition = true)
        {
            int typeSize = length;
            var value = byteList.GetRange(CurrentIndex, length).ToArray();
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public bool ReadBool(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(bool);
            var value = BitConverter.ToBoolean(new byte[] { byteList[CurrentIndex] }, 0);
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public char ReadChar(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(char);
            var value = BitConverter.ToChar(new byte[] { byteList[CurrentIndex] }, 0);
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public double ReadDouble(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(double);
            var value = BitConverter.ToDouble(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public float ReadFloat(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(float);
            var value = BitConverter.ToSingle(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public int ReadInt(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(int);
            var value = BitConverter.ToInt32(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public long ReadLong(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(long);
            var value = BitConverter.ToInt64(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public short ReadShort(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(short);
            var value = BitConverter.ToInt16(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public uint ReadUInt(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(uint);
            var value = BitConverter.ToUInt32(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public ulong ReadULong(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(ulong);
            var value = BitConverter.ToUInt64(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public ushort ReadUShort(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(short);
            var value = BitConverter.ToUInt16(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
            CurrentIndex += moveIndexPosition ? typeSize : 0;
            return value;
        }

        public string ReadString(bool moveIndexPosition = true)
        {
            int strLen = ReadInt(false);
            var value = Encoding.UTF8.GetString(byteList.GetRange(CurrentIndex + 4, strLen).ToArray());
            CurrentIndex += moveIndexPosition ? strLen + 4 : 0;
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
                    CurrentIndex = 0;
                    byteList = null;
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
