using System;
using System.Collections.Generic;
using System.Reflection;

namespace CNet
{
    internal class SerializeManager
    {
        public static SerializeManager Instance { get; } = new SerializeManager();

        private readonly Dictionary<Type, Action<NetPacket, object, FieldInfo>> writeFieldActions = new Dictionary<Type, Action<NetPacket, object, FieldInfo>>();
        private readonly Dictionary<Type, Func<NetPacket, object>> readFieldActions = new Dictionary<Type, Func<NetPacket, object>>();
        private readonly Dictionary<Type, Action<NetPacket, object, PropertyInfo>> writePropertyActions = new Dictionary<Type, Action<NetPacket, object, PropertyInfo>>();
        private readonly Dictionary<Type, Func<NetPacket, object>> readPropertyActions = new Dictionary<Type, Func<NetPacket, object>>();

        private readonly Dictionary<Type, MemberInfo[]> membersCache = new Dictionary<Type, MemberInfo[]>();

        private SerializeManager()
        {
            AddReadActions();
            AddWriteActions();
            CacheMembers();
        }

        public void Write<T>(NetPacket packet, T obj)
        {
            Write(packet, typeof(T), obj);
        }

        private void Write(NetPacket packet, Type type, object obj)
        {
            if (!membersCache.ContainsKey(type))
            {
                throw new Exception((type.IsClass ? "Class" : "Struct") + " " + type + " is not a registered Syncable type.");
            }
            MemberInfo[] members = membersCache[type];

            foreach (var member in members)
            {
                switch (member.MemberType)
                {
                    case MemberTypes.Field:
                        {
                            FieldInfo field = (FieldInfo)member;
                            if (field.FieldType.GetCustomAttribute<NetSyncableAttribute>() != null)
                            {
                                var childSyncable = field.GetValue(obj);
                                Write(packet, field.FieldType, childSyncable);
                            }
                            else
                            {
                                writeFieldActions[field.FieldType].Invoke(packet, obj, field);
                            }
                            break;
                        }
                    case MemberTypes.Property:
                        {
                            PropertyInfo property = (PropertyInfo)member;
                            if (property.PropertyType.GetCustomAttribute<NetSyncableAttribute>() != null)
                            {
                                var childSyncable = property.GetValue(obj);
                                Write(packet, property.PropertyType, childSyncable);
                            }
                            else
                            {
                                writePropertyActions[property.PropertyType].Invoke(packet, obj, property);
                            }
                            break;
                        }
                }
            }
        }

        public T Read<T>(NetPacket packet)
        {
            return (T)Read(typeof(T), packet);
        }

        private object Read(Type type, NetPacket packet)
        {
            if (!membersCache.ContainsKey(type))
            {
                throw new Exception((type.IsClass ? "Class" : "Struct") + " " + type + " is not a registered Syncable type.");
            }
            MemberInfo[] members = membersCache[type];
            var obj = Activator.CreateInstance(type);

            foreach (var member in members)
            {
                switch (member.MemberType)
                {
                    case MemberTypes.Field:
                        {
                            FieldInfo field = (FieldInfo)member;
                            if (field.FieldType.GetCustomAttribute<NetSyncableAttribute>() != null)
                            {
                                var childSyncable = Read(field.FieldType, packet);
                                field.SetValueDirect(__makeref(obj), childSyncable);
                            }
                            else
                            {
                                field.SetValueDirect(__makeref(obj), readFieldActions[field.FieldType].Invoke(packet));
                            }
                            break;
                        }
                    case MemberTypes.Property:
                        {
                            PropertyInfo property = (PropertyInfo)member;
                            if (property.PropertyType.GetCustomAttribute<NetSyncableAttribute>() != null)
                            {
                                var childSyncable = Read(property.PropertyType, packet);
                                property.SetValue(obj, childSyncable);
                            }
                            else
                            {
                                property.SetValue(obj, readPropertyActions[property.PropertyType].Invoke(packet));
                            }
                            break;
                        }
                }
            }

            return obj;
        }

        private void CacheMembers()
        {
            Assembly assembly = Assembly.GetEntryAssembly();

            foreach (Type type in assembly.GetTypes())
            {
                if (type.GetCustomAttribute<NetSyncableAttribute>() == null)
                {
                    continue;
                }

                CheckFields(type);
                CheckProperties(type);

                MemberInfo[] members = Array.FindAll(type.GetMembers(type.GetCustomAttribute<NetSyncableAttribute>().BindingFlags), member =>
                    member is FieldInfo || member is PropertyInfo
                );
                membersCache[type] = members;
            }
        }

        private void CheckFields(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (FieldInfo f in fields)
            {
                if (!(f.FieldType.GetCustomAttribute<NetSyncableAttribute>() != null || f.FieldType.IsPrimitive || (f.FieldType.IsArray && f.FieldType.GetElementType().IsPrimitive) || f.FieldType == typeof(string) || f.FieldType == typeof(string[])))
                {
                    throw new Exception("Unsupported Syncable type in " + (type.IsClass ? "class" : "struct") + " '" + type.Name + "'. Only other NetSyncable types, primitive types, arrays of primitive types, and strings are supported.");
                }
            }
        }

        private void CheckProperties(Type type)
        {
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo p in properties)
            {
                if (!(p.PropertyType.GetCustomAttribute<NetSyncableAttribute>() != null || p.PropertyType.IsPrimitive || (p.PropertyType.IsArray && p.PropertyType.GetElementType().IsPrimitive) || p.PropertyType == typeof(string) || p.PropertyType == typeof(string[])))
                {
                    throw new Exception("Unsupported Syncable type in " + (type.IsClass ? "class" : "struct") + " '" + type.Name + "'. Only other NetSyncable types, primitive types, arrays of primitive types, and strings are supported.");
                }
            }
        }

        private void AddReadActions()
        {
            readFieldActions[typeof(byte)] = (packet) => { return packet.ReadByte(); };
            readFieldActions[typeof(sbyte)] = (packet) => { return packet.ReadSByte(); };
            readFieldActions[typeof(bool)] = (packet) => { return packet.ReadBool(); };
            readFieldActions[typeof(char)] = (packet) => { return packet.ReadChar(); };
            readFieldActions[typeof(double)] = (packet) => { return packet.ReadDouble(); };
            readFieldActions[typeof(float)] = (packet) => { return packet.ReadFloat(); };
            readFieldActions[typeof(int)] = (packet) => { return packet.ReadInt(); };
            readFieldActions[typeof(long)] = (packet) => { return packet.ReadLong(); };
            readFieldActions[typeof(short)] = (packet) => { return packet.ReadShort(); };
            readFieldActions[typeof(uint)] = (packet) => { return packet.ReadUInt(); };
            readFieldActions[typeof(ulong)] = (packet) => { return packet.ReadULong(); };
            readFieldActions[typeof(ushort)] = (packet) => { return packet.ReadUShort(); };
            readFieldActions[typeof(string)] = (packet) => { return packet.ReadString(); };
            readFieldActions[typeof(byte[])] = (packet) => { return packet.ReadBytes(); };
            readFieldActions[typeof(sbyte[])] = (packet) => { return packet.ReadSBytes(); };
            readFieldActions[typeof(bool[])] = (packet) => { return packet.ReadBools(); };
            readFieldActions[typeof(char[])] = (packet) => { return packet.ReadChars(); };
            readFieldActions[typeof(double[])] = (packet) => { return packet.ReadDoubles(); };
            readFieldActions[typeof(float[])] = (packet) => { return packet.ReadFloats(); };
            readFieldActions[typeof(int[])] = (packet) => { return packet.ReadInts(); };
            readFieldActions[typeof(long[])] = (packet) => { return packet.ReadLongs(); };
            readFieldActions[typeof(short[])] = (packet) => { return packet.ReadShorts(); };
            readFieldActions[typeof(uint[])] = (packet) => { return packet.ReadUInts(); };
            readFieldActions[typeof(ulong[])] = (packet) => { return packet.ReadULongs(); };
            readFieldActions[typeof(ushort[])] = (packet) => { return packet.ReadUShorts(); };
            readFieldActions[typeof(string[])] = (packet) => { return packet.ReadStrings(); };

            readPropertyActions[typeof(byte)] = (packet) => { return packet.ReadByte(); };
            readPropertyActions[typeof(sbyte)] = (packet) => { return packet.ReadSByte(); };
            readPropertyActions[typeof(bool)] = (packet) => { return packet.ReadBool(); };
            readPropertyActions[typeof(char)] = (packet) => { return packet.ReadChar(); };
            readPropertyActions[typeof(double)] = (packet) => { return packet.ReadDouble(); };
            readPropertyActions[typeof(float)] = (packet) => { return packet.ReadFloat(); };
            readPropertyActions[typeof(int)] = (packet) => { return packet.ReadInt(); };
            readPropertyActions[typeof(long)] = (packet) => { return packet.ReadLong(); };
            readPropertyActions[typeof(short)] = (packet) => { return packet.ReadShort(); };
            readPropertyActions[typeof(uint)] = (packet) => { return packet.ReadUInt(); };
            readPropertyActions[typeof(ulong)] = (packet) => { return packet.ReadULong(); };
            readPropertyActions[typeof(ushort)] = (packet) => { return packet.ReadUShort(); };
            readPropertyActions[typeof(string)] = (packet) => { return packet.ReadString(); };
            readPropertyActions[typeof(byte[])] = (packet) => { return packet.ReadBytes(); };
            readPropertyActions[typeof(sbyte[])] = (packet) => { return packet.ReadSBytes(); };
            readPropertyActions[typeof(bool[])] = (packet) => { return packet.ReadBools(); };
            readPropertyActions[typeof(char[])] = (packet) => { return packet.ReadChars(); };
            readPropertyActions[typeof(double[])] = (packet) => { return packet.ReadDoubles(); };
            readPropertyActions[typeof(float[])] = (packet) => { return packet.ReadFloats(); };
            readPropertyActions[typeof(int[])] = (packet) => { return packet.ReadInts(); };
            readPropertyActions[typeof(long[])] = (packet) => { return packet.ReadLongs(); };
            readPropertyActions[typeof(short[])] = (packet) => { return packet.ReadShorts(); };
            readPropertyActions[typeof(uint[])] = (packet) => { return packet.ReadUInts(); };
            readPropertyActions[typeof(ulong[])] = (packet) => { return packet.ReadULongs(); };
            readPropertyActions[typeof(ushort[])] = (packet) => { return packet.ReadUShorts(); };
            readPropertyActions[typeof(string[])] = (packet) => { return packet.ReadStrings(); };
        }

        private void AddWriteActions()
        {
            writeFieldActions[typeof(byte)] = (packet, obj, field) => packet.Write((byte)field.GetValue(obj));
            writeFieldActions[typeof(sbyte)] = (packet, obj, field) => packet.Write((sbyte)field.GetValue(obj));
            writeFieldActions[typeof(bool)] = (packet, obj, field) => packet.Write((bool)field.GetValue(obj));
            writeFieldActions[typeof(char)] = (packet, obj, field) => packet.Write((char)field.GetValue(obj));
            writeFieldActions[typeof(double)] = (packet, obj, field) => packet.Write((double)field.GetValue(obj));
            writeFieldActions[typeof(float)] = (packet, obj, field) => packet.Write((float)field.GetValue(obj));
            writeFieldActions[typeof(int)] = (packet, obj, field) => packet.Write((int)field.GetValue(obj));
            writeFieldActions[typeof(long)] = (packet, obj, field) => packet.Write((long)field.GetValue(obj));
            writeFieldActions[typeof(short)] = (packet, obj, field) => packet.Write((short)field.GetValue(obj));
            writeFieldActions[typeof(uint)] = (packet, obj, field) => packet.Write((uint)field.GetValue(obj));
            writeFieldActions[typeof(ulong)] = (packet, obj, field) => packet.Write((ulong)field.GetValue(obj));
            writeFieldActions[typeof(ushort)] = (packet, obj, field) => packet.Write((ushort)field.GetValue(obj));
            writeFieldActions[typeof(string)] = (packet, obj, field) => packet.Write((string)field.GetValue(obj));
            writeFieldActions[typeof(byte[])] = (packet, obj, field) => packet.Write((byte[])field.GetValue(obj));
            writeFieldActions[typeof(sbyte[])] = (packet, obj, field) => packet.Write((sbyte[])field.GetValue(obj));
            writeFieldActions[typeof(bool[])] = (packet, obj, field) => packet.Write((bool[])field.GetValue(obj));
            writeFieldActions[typeof(char[])] = (packet, obj, field) => packet.Write((char[])field.GetValue(obj));
            writeFieldActions[typeof(double[])] = (packet, obj, field) => packet.Write((double[])field.GetValue(obj));
            writeFieldActions[typeof(float[])] = (packet, obj, field) => packet.Write((float[])field.GetValue(obj));
            writeFieldActions[typeof(int[])] = (packet, obj, field) => packet.Write((int[])field.GetValue(obj));
            writeFieldActions[typeof(long[])] = (packet, obj, field) => packet.Write((long[])field.GetValue(obj));
            writeFieldActions[typeof(short[])] = (packet, obj, field) => packet.Write((short[])field.GetValue(obj));
            writeFieldActions[typeof(uint[])] = (packet, obj, field) => packet.Write((uint[])field.GetValue(obj));
            writeFieldActions[typeof(ulong[])] = (packet, obj, field) => packet.Write((ulong[])field.GetValue(obj));
            writeFieldActions[typeof(ushort[])] = (packet, obj, field) => packet.Write((ushort[])field.GetValue(obj));
            writeFieldActions[typeof(string[])] = (packet, obj, field) => packet.Write((string[])field.GetValue(obj));

            writePropertyActions[typeof(byte)] = (packet, obj, property) => packet.Write((byte)property.GetValue(obj));
            writePropertyActions[typeof(sbyte)] = (packet, obj, property) => packet.Write((sbyte)property.GetValue(obj));
            writePropertyActions[typeof(bool)] = (packet, obj, property) => packet.Write((bool)property.GetValue(obj));
            writePropertyActions[typeof(char)] = (packet, obj, property) => packet.Write((char)property.GetValue(obj));
            writePropertyActions[typeof(double)] = (packet, obj, property) => packet.Write((double)property.GetValue(obj));
            writePropertyActions[typeof(float)] = (packet, obj, property) => packet.Write((float)property.GetValue(obj));
            writePropertyActions[typeof(int)] = (packet, obj, property) => packet.Write((int)property.GetValue(obj));
            writePropertyActions[typeof(long)] = (packet, obj, property) => packet.Write((long)property.GetValue(obj));
            writePropertyActions[typeof(short)] = (packet, obj, property) => packet.Write((short)property.GetValue(obj));
            writePropertyActions[typeof(uint)] = (packet, obj, property) => packet.Write((uint)property.GetValue(obj));
            writePropertyActions[typeof(ulong)] = (packet, obj, property) => packet.Write((ulong)property.GetValue(obj));
            writePropertyActions[typeof(ushort)] = (packet, obj, property) => packet.Write((ushort)property.GetValue(obj));
            writePropertyActions[typeof(string)] = (packet, obj, property) => packet.Write((string)property.GetValue(obj));
            writePropertyActions[typeof(byte[])] = (packet, obj, property) => packet.Write((byte[])property.GetValue(obj));
            writePropertyActions[typeof(sbyte[])] = (packet, obj, property) => packet.Write((sbyte[])property.GetValue(obj));
            writePropertyActions[typeof(bool[])] = (packet, obj, property) => packet.Write((bool[])property.GetValue(obj));
            writePropertyActions[typeof(char[])] = (packet, obj, property) => packet.Write((char[])property.GetValue(obj));
            writePropertyActions[typeof(double[])] = (packet, obj, property) => packet.Write((double[])property.GetValue(obj));
            writePropertyActions[typeof(float[])] = (packet, obj, property) => packet.Write((float[])property.GetValue(obj));
            writePropertyActions[typeof(int[])] = (packet, obj, property) => packet.Write((int[])property.GetValue(obj));
            writePropertyActions[typeof(long[])] = (packet, obj, property) => packet.Write((long[])property.GetValue(obj));
            writePropertyActions[typeof(short[])] = (packet, obj, property) => packet.Write((short[])property.GetValue(obj));
            writePropertyActions[typeof(uint[])] = (packet, obj, property) => packet.Write((uint[])property.GetValue(obj));
            writePropertyActions[typeof(ulong[])] = (packet, obj, property) => packet.Write((ulong[])property.GetValue(obj));
            writePropertyActions[typeof(ushort[])] = (packet, obj, property) => packet.Write((ushort[])property.GetValue(obj));
            writePropertyActions[typeof(string[])] = (packet, obj, property) => packet.Write((string[])property.GetValue(obj));
        }
    }
}