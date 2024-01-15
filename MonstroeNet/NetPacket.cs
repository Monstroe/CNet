using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace MonstroeNet
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

        public void Write(byte value)   { byteList.Add(value); }
        public void Write(byte[] value) { byteList.AddRange(value); }
        public void Write(bool value)   { byteList.AddRange(BitConverter.GetBytes(value)); }
        public void Write(char value)   { byteList.AddRange(BitConverter.GetBytes(value)); }
        public void Write(double value) { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(float value)  { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(int value)    { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(long value)   { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(short value)  { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(uint value)   { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(ulong value)  { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(ushort value) { byteList.AddRange(ToProperEndian(BitConverter.GetBytes(value))); }
        public void Write(string value) 
        {
            Write(value.Length);
            //byteList.AddRange(ToProperEndian(Encoding.ASCII.GetBytes(value)));
            byteList.AddRange(Encoding.UTF8.GetBytes(value));
        }

        private byte[] ToProperEndian(byte[] value)
        {
            if(BitConverter.IsLittleEndian)
            {
                Array.Reverse(value);
            }
            return value;
        }

        public byte ReadByte(bool moveIndexPosition = true)
        {
            int typeSize = 1;

            //if (byteList.Count - CurrentIndex > typeSize - 1)
            //{
                var value = byteList[CurrentIndex];
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //    throw new Exception("Could not read value of type 'byte'!");
            //}
        }

        public byte[] ReadBytes(int length, bool moveIndexPosition = true)
        {
            int typeSize = length;

            //if (byteList.Count - CurrentIndex > typeSize - 1)
            //{
                var value = byteList.GetRange(CurrentIndex, length).ToArray();
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //    throw new Exception("Could not read value of type 'byte[]'!");
            //}
        }

        public bool ReadBool(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(bool);

            //if (byteList.Count - CurrentIndex > typeSize - 1)
            //{
                //var value = BitConverter.ToBoolean(ByteArray, CurrentIndex);
                var value = BitConverter.ToBoolean(new byte[] { byteList[CurrentIndex] }, 0);
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //    throw new Exception("Could not read value of type 'bool'!");
            //}
        }

        public char ReadChar(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(char);

            //if (byteList.Count - CurrentIndex > typeSize - 1)
            //{
                //var value = BitConverter.ToChar(ByteArray, CurrentIndex);
                var value = BitConverter.ToChar(new byte[] { byteList[CurrentIndex] }, 0);
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //    throw new Exception("Could not read value of type 'char'!");
            //}
        }

        public double ReadDouble(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(double);

            //if (byteList.Count - CurrentIndex > typeSize - 1)
            //{
                //byte[] byteVal = new byte[typeSize];
                //Array.Copy(ByteArray, CurrentIndex, byteVal, 0, typeSize);
                //var value = BitConverter.ToDouble(ToProperEndian(byteVal), 0);
                var value = BitConverter.ToDouble(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0); 
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //    throw new Exception("Could not read value of type 'double'!");
            //}
        }

        public float ReadFloat(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(float);

            //if (byteList.Count - CurrentIndex > typeSize - 1)
            //{
                //byte[] byteVal = new byte[typeSize];
                //Array.Copy(ByteArray, CurrentIndex, byteVal, 0, typeSize);
                //var value = BitConverter.ToSingle(ToProperEndian(byteVal), 0);
                var value = BitConverter.ToSingle(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //    throw new Exception("Could not read value of type 'float'!");
            //}
        }

        public int ReadInt(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(int);

            //if(byteList.Count - CurrentIndex > typeSize - 1)
            //{
                //byte[] byteVal = new byte[typeSize];
                //Array.Copy(ByteArray, CurrentIndex, byteVal, 0, typeSize);
                //var value = BitConverter.ToInt32(ToProperEndian(byteVal), 0);
                var value = BitConverter.ToInt32(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //    throw new Exception("Could not read value of type 'int'!");
            //}
        }

        public long ReadLong(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(long);

            //if (byteList.Count - CurrentIndex > typeSize - 1)
            //{
                //byte[] byteVal = new byte[typeSize];
                //Array.Copy(ByteArray, CurrentIndex, byteVal, 0, typeSize);
                //var value = BitConverter.ToInt64(ToProperEndian(byteVal), 0);
                var value = BitConverter.ToInt64(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //   throw new Exception("Could not read value of type 'long'!");
            //}
        }

        public short ReadShort(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(short);

            //if (byteList.Count - CurrentIndex > typeSize - 1)
            //{
                //byte[] byteVal = new byte[typeSize];
                //Array.Copy(ByteArray, CurrentIndex, byteVal, 0, typeSize);
                //var value = BitConverter.ToInt16(ToProperEndian(byteVal), 0);
                var value = BitConverter.ToInt16(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //    throw new Exception("Could not read value of type 'short'!");
            //}
        }

        public uint ReadUInt(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(uint);

            //if (byteList.Count - CurrentIndex > typeSize - 1)
            //{
                //byte[] byteVal = new byte[typeSize];
                //Array.Copy(ByteArray, CurrentIndex, byteVal, 0, typeSize);
                //var value = BitConverter.ToUInt32(ToProperEndian(byteVal), 0);
                var value = BitConverter.ToUInt32(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //    throw new Exception("Could not read value of type 'uint'!");
            //}
        }

        public ulong ReadULong(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(ulong);

            //if (byteList.Count - CurrentIndex > typeSize - 1)
            //{
                //byte[] byteVal = new byte[typeSize];
                //Array.Copy(ByteArray, CurrentIndex, byteVal, 0, typeSize);
                //var value = BitConverter.ToUInt64(ToProperEndian(byteVal), 0);
                var value = BitConverter.ToUInt64(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //    throw new Exception("Could not read value of type 'ulong'!");
            //}
        }

        public ushort ReadUShort(bool moveIndexPosition = true)
        {
            int typeSize = sizeof(short);

            //if (byteList.Count - CurrentIndex > typeSize - 1)
            //{
                //byte[] byteVal = new byte[typeSize];
                //Array.Copy(ByteArray, CurrentIndex, byteVal, 0, typeSize);
                //var value = BitConverter.ToUInt16(ToProperEndian(byteVal), 0);
                var value = BitConverter.ToUInt16(ToProperEndian(byteList.GetRange(CurrentIndex, typeSize).ToArray()), 0);
                CurrentIndex += moveIndexPosition ? typeSize : 0;
                return value;
            //}
            //else
            //{
            //    throw new Exception("Could not read value of type 'ushort'!");
            //}
        }

        public string ReadString(bool moveIndexPosition = true)
        {
            //try
            //{
                //int strLen = ReadInt(false);
                int strLen = ReadInt(false);
                //var value = Encoding.ASCII.GetString(ByteArray, CurrentIndex + 4, strLen);
                //var value = Encoding.ASCII.GetString(ByteArray, CurrentIndex, strLen);
                var value = Encoding.UTF8.GetString(byteList.GetRange(CurrentIndex + 4, strLen).ToArray());
                //CurrentIndex += moveIndexPosition ? strLen + 4 : 0;
                CurrentIndex += moveIndexPosition ? strLen + 4 : 0;
                return value;
            //}
            //catch
            //{
            //    throw new Exception("Could not read value of type 'string'!");
            //}
        }



        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(!disposed)
            {
                if(disposing)
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
