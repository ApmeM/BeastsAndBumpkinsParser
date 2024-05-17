using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;

namespace test;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);

        writer.Write('a');
        writer.Write('b');
        writer.Write('c');

        output.Seek(0, SeekOrigin.Begin);

        writer.Write('d');
        writer.Write('e');

        output.Seek(0, SeekOrigin.Begin);
        using var outr = new BinaryReader(output, Encoding.ASCII);

        CollectionAssert.AreEqual(new[] { 'd', 'e', 'c' }, outr.ReadChars(100));
    }

    [Test]
    public void Test2()
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);

        writer.Write(new[] { 'a', 'b', 'c' });

        output.Seek(0, SeekOrigin.Begin);

        writer.Write('d');
        writer.Write('e');

        output.Seek(0, SeekOrigin.Begin);
        using var outr = new BinaryReader(output, Encoding.ASCII);

        CollectionAssert.AreEqual(new[] { 'd', 'e', 'c' }, outr.ReadChars(100));
    }


    [Test]
    public void Test3()
    {
        mtFloatInt Sample = new mtFloatInt();
        Sample.Int = 0;
        Assert.AreEqual(Sample.Float, 0);
        Sample.Int = 0x4B400000; // 12582912.0f
        Assert.AreEqual(Sample.Float, 12582912.0f);
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct mtFloatInt
    {
        [FieldOffset(0)] public float Float;
        [FieldOffset(0)] public int Int;
    }

}