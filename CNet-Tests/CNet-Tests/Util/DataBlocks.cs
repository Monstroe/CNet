using System.Collections;
using System.Reflection;
using CNet;

namespace CNet_Tests;

[NetSyncable(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)]
public class NetClass     // 418 bytes
{
    public bool BoolValue { get; set; } = true;                                     // 1 byte
    private byte byteValue = 1;                                                     // 1 byte
    public sbyte SByteValue { get; set; } = -1;                                     // 1 byte
    private char charValue = 'A';                                                   // 2 byte
    public short ShortValue { get; set; } = -123;                                   // 2 bytes
    private ushort uShortValue = 123;                                               // 2 bytes
    public int IntValue { get; set; } = -123456;                                    // 4 bytes
    private uint uIntValue = 123456;                                                // 4 bytes
    public long LongValue { get; set; } = -123456789;                               // 8 bytes
    private ulong uLongValue = 123456789;                                           // 8 bytes
    public float FloatValue { get; set; } = 123.456f;                               // 4 bytes
    private double uDoubleValue = 123.456789;                                       // 8 bytes
    public string StringValue { get; set; } = "Test";                               // 8 bytes
    public NetStruct NetStructValue { get; set; } = new NetStruct();                // 53 bytes

    private bool[] boolArray = new bool[] { true, false, true };                    // 7 bytes
    public byte[] ByteArray { get; set; } = new byte[] { 1, 2, 3 };                 // 7 bytes
    private sbyte[] sByteArray = new sbyte[] { -1, 0, 1 };                          // 7 bytes
    public char[] CharArray { get; set; } = new char[] { 'A', 'B', 'C' };           // 10 bytes
    private short[] shortArray = new short[] { -1, 0, 1 };                          // 10 bytes
    public ushort[] UShortArray { get; set; } = new ushort[] { 1, 2, 3 };           // 10 bytes
    private int[] intArray = new int[] { -1, 0, 1 };                                // 16 bytes
    public uint[] UIntArray { get; set; } = new uint[] { 1, 2, 3 };                 // 16 bytes
    private long[] longArray = new long[] { -1, 0, 1 };                             // 28 bytes
    public ulong[] ULongArray { get; set; } = new ulong[] { 1, 2, 3 };              // 28 bytes
    private float[] floatArray = new float[] { -1.0f, 0.0f, 1.0f };                 // 16 bytes
    public double[] DoubleArray { get; set; } = new double[] { -1.0, 0.0, 1.0 };    // 28 bytes
    private string[] stringArray = new string[] { "A", "B", "C" };                  // 19 bytes  
    public NetStruct[] NetStructArray { get; set; } = new NetStruct[] { new NetStruct(), new NetStruct() }; // 110 bytes
}

[NetSyncable(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)]
public struct NetStruct     // 53 bytes
{
    public NetStruct()
    {
    }

    public bool BoolValue { get; set; } = true;                                     // 1 byte
    private byte byteValue = 1;                                                     // 1 byte
    public sbyte SByteValue { get; set; } = -1;                                     // 1 byte
    private char charValue = 'A';                                                   // 2 byte
    public short ShortValue { get; set; } = -123;                                   // 2 bytes
    private ushort uShortValue = 123;                                               // 2 bytes
    public int IntValue { get; set; } = -123456;                                    // 4 bytes
    private uint uIntValue = 123456;                                                // 4 bytes
    public long LongValue { get; set; } = -123456789;                               // 8 bytes
    private ulong uLongValue = 123456789;                                           // 8 bytes
    public float FloatValue { get; set; } = 123.456f;                               // 4 bytes
    private double uDoubleValue = 123.456789;                                       // 8 bytes
    public string StringValue { get; set; } = "Test";                               // 8 bytes
}