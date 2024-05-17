using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BeastsAndBumpkinsParser
{
    public class M10File : IFile
    {
        private string FileName;

        private byte[] data;

        public M10File(string fileName, byte[] data)
        {
            this.FileName = fileName;

            // if (!fileName.Contains("monkf5") || data.Length != 8528)
            // {
            //     this.data = new byte[0];
            //     return;
            // }

            using var input = new MemoryStream(data);
            using var reader = new BinaryReader(input, Encoding.ASCII);

            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);

            if (Encoding.ASCII.GetString(reader.ReadBytes(2)) != "PT")
            {
                Console.WriteLine("Missing PT signature.");
                return;
            }

            // Read the PT header
            var samplesCount = ParsePTHeader(reader);
            var decoderStruct = new M10DecoderPrivate();
            InitializeDecoder(decoderStruct, reader);

            WriteWaveHeader(writer, 22050, 16, 1, samplesCount);

            this.Decode(decoderStruct, writer, samplesCount);

            output.Seek(0, SeekOrigin.Begin);
            this.data = output.ToArray();
        }

        void WriteWaveHeader(BinaryWriter writer, uint sampleRate, ushort bitsPerSample, ushort channels, uint numberSamples)
        {
            // Write some RIFF information
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write((uint)(numberSamples * (bitsPerSample / 8) + 36));
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write((uint)16);

            // Write the format chunk
            writer.Write((ushort)1);
            writer.Write((ushort)channels);
            writer.Write((uint)sampleRate);
            writer.Write((uint)bitsPerSample / 8 * sampleRate * channels);
            writer.Write((ushort)(bitsPerSample / 8 * channels));
            writer.Write((ushort)bitsPerSample);

            // Write the data information
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write((uint)(numberSamples * (bitsPerSample / 8)));
        }

        uint ParsePTHeader(BinaryReader reader)
        {
            var totalSampleCount = 0u;
            reader.ReadBytes(2);

            // Data offset
            var dataOffset = reader.ReadUInt32();

            // Read in the header (code borrowed from Valery V. Anisimovsky (samael@avn.mccme.ru) )
            var inHeader = true;
            while (inHeader)
            {
                var value = reader.ReadByte();
                switch (value) // parse header code
                {
                    case 0xFF: // end of header
                        inHeader = false;
                        break;
                    case 0xFE: // skip
                    case 0xFC: // skip
                        break;
                    case 0xFD: // subheader starts...
                        var inSubHeader = true;
                        while (inSubHeader)
                        {
                            value = reader.ReadByte();
                            switch (value) // parse subheader code
                            {
                                case 0x83:
                                    value = reader.ReadByte();
                                    var compressionType = ReadBytes(reader, value);
                                    break;
                                case 0x85:
                                    value = reader.ReadByte();
                                    totalSampleCount = ReadBytes(reader, value);
                                    break;
                                case 0xFF:
                                    break;
                                case 0x8A: // end of subheader
                                    inSubHeader = false;
                                    value = reader.ReadByte();
                                    reader.BaseStream.Seek(value, SeekOrigin.Current);
                                    break;
                                default: // ???
                                    value = reader.ReadByte();
                                    reader.BaseStream.Seek(value, SeekOrigin.Current);
                                    break;
                            }
                        }
                        break;
                    default:
                        value = reader.ReadByte();
                        if (value == 0xFF)
                        {
                            reader.ReadBytes(4);
                        }
                        reader.ReadByte();
                        break;
                }
            }

            // Seek to the data offset
            reader.BaseStream.Seek(dataOffset, SeekOrigin.Begin);
            return totalSampleCount;
        }

        uint ReadBytes(BinaryReader reader, byte count)
        {
            var result = 0u;
            for (var i = 0; i < count; i++)
            {
                var value = reader.ReadByte();
                result <<= 8;
                result |= value;
            }
            return result;
        }

        void InitializeDecoder(M10DecoderPrivate decoderStruct, BinaryReader reader)
        {
            decoderStruct.CurrentBits = reader.ReadByte();
            decoderStruct.CompressedData = reader;
            decoderStruct.BitCount = 8;
            decoderStruct.FirstBit = GetBits(decoderStruct, 1);
            decoderStruct.Second4Bits = 32 - GetBits(decoderStruct, 4);
            decoderStruct.FloatTable[0] = (float)(GetBits(decoderStruct, 4) + 1) * 8.0f;

            var incCoef = 1.04 + GetBits(decoderStruct, 6) * 0.001;
            for (var i = 0; i < 63; i++)
            {
                decoderStruct.FloatTable[i + 1] = (float)(decoderStruct.FloatTable[i] * incCoef);
            }
        }

        void Decode(M10DecoderPrivate decoderStruct, BinaryWriter writer, uint samplesCount)
        {
            var sampleBufferOffset = 432;

            for (var i = 0u; i < samplesCount; i++)
            {
                var sample = new FloatInt
                {
                    Int = 0x4B400000
                };

                if (sampleBufferOffset >= 432)
                {
                    DecodeBlock(decoderStruct);
                    sampleBufferOffset = 0;
                }

                sample.Float += decoderStruct.SampleBuffer[sampleBufferOffset];
                sampleBufferOffset++;

                var clipped = sample.Int & 0x1FFFF;
                if (clipped > 0x7FFF && clipped < 0x18000)
                {
                    if (clipped >= 0x10000)
                    {
                        clipped = 0x8000;
                    }
                    else
                    {
                        clipped = 0x7FFF;
                    }
                }

                writer.Write((short)clipped);
            }
        }

        void DecodeBlock(M10DecoderPrivate decoderStruct)
        {
            var tableA = new float[12];
            var tableB = new float[118];

            var bits = GetBits(decoderStruct, 6);

            var flag = bits < decoderStruct.Second4Bits ? 1 : 0;

            tableA[0] = (FloatLookupTable[bits] - decoderStruct.Table1[0]) * 0.25f;

            for (uint i = 1; i < 4; i++)
            {
                bits = GetBits(decoderStruct, 6);

                tableA[i] = (FloatLookupTable[bits] - decoderStruct.Table1[i]) * 0.25f;
            }

            for (uint i = 4; i < 12; i++)
            {
                bits = GetBits(decoderStruct, 5);

                tableA[i] = (FloatLookupTable[bits + 16] - decoderStruct.Table1[i]) * 0.25f;
            }

            var curSampleBufPtr = 0;

            for (var i = 216; i < 648; i += 108)
            {
                var bigTableIndex = i - GetBits(decoderStruct, 8);

                var someFloat = GetBits(decoderStruct, 4) * 2.0f / 30.0f;
                var someOtherFloat = decoderStruct.FloatTable[GetBits(decoderStruct, 6)];

                if (decoderStruct.FirstBit == 0)
                {
                    FunctionThree(decoderStruct, flag, tableB, 5, 1);
                }
                else
                {
                    var indexAdjust = GetBits(decoderStruct, 1);
                    bits = GetBits(decoderStruct, 1);

                    FunctionThree(decoderStruct, flag, tableB, 5 + (int)indexAdjust, 2);

                    if (bits != 0)
                    {
                        for (var j = 0; j < 108; j += 2)
                        {
                            tableB[j + 6 - indexAdjust] = 0;
                        }
                    }
                    else
                    {
                        for (var j = 0; j < 5; j++)
                        {
                            tableB[j] = 0;
                            tableB[j + 113] = 0;
                        }

                        FunctionOne(tableB, 6 - (int)indexAdjust);
                        someOtherFloat *= 0.5f;
                    }
                }

                for (var j = 0; j < 108; j++)
                {
                    var a = someOtherFloat * tableB[j + 5];
                    var b = someFloat * decoderStruct.BigTable[bigTableIndex + j];
                    decoderStruct.SampleBuffer[curSampleBufPtr] = a + b;
                    curSampleBufPtr++;
                }
            }

            for (var i = 0; i < 324; i++)
            {
                decoderStruct.BigTable[i] = decoderStruct.SampleBuffer[i + 108];
            }

            for (var i = 0; i < 12; i++)
            {
                decoderStruct.Table1[i] += tableA[i];
            }
            FunctionFour(decoderStruct, 0, 1);

            for (var i = 0; i < 12; i++)
            {
                decoderStruct.Table1[i] += tableA[i];
            }
            FunctionFour(decoderStruct, 12, 1);

            for (var i = 0; i < 12; i++)
            {
                decoderStruct.Table1[i] += tableA[i];
            }
            FunctionFour(decoderStruct, 24, 1);

            for (var i = 0; i < 12; i++)
            {
                decoderStruct.Table1[i] += tableA[i];
            }
            FunctionFour(decoderStruct, 36, 33);
        }

        // ================================= Functions ===============================

        uint[] BitmaskLookupTable = new uint[] { 0, 1, 3, 7, 0x0F, 0x1F, 0x3F, 0x7F, 0x0FF };

        byte[] ByteLookupTable = new byte[]{
                    4, 6, 5, 9, 4, 6, 5, 0x0D, 4, 6, 5, 0x0A, 4, 6, 5, 0x11,
                    4, 6, 5, 9, 4, 6, 5, 0x0E, 4, 6, 5, 0x0A, 4, 6, 5, 0x15,
                    4, 6, 5, 9, 4, 6, 5, 0x0D, 4, 6, 5, 0x0A, 4, 6, 5, 0x12,
                    4, 6, 5, 9, 4, 6, 5, 0x0E, 4, 6, 5, 0x0A, 4, 6, 5, 0x19,
                    4, 6, 5, 9, 4, 6, 5, 0x0D, 4, 6, 5, 0x0A, 4, 6, 5, 0x11,
                    4, 6, 5, 9, 4, 6, 5, 0x0E, 4, 6, 5, 0x0A, 4, 6, 5, 0x16,
                    4, 6, 5, 9, 4, 6, 5, 0x0D, 4, 6, 5, 0x0A, 4, 6, 5, 0x12,
                    4, 6, 5, 9, 4, 6, 5, 0x0E, 4, 6, 5, 0x0A, 4, 6, 5, 0,
                    4, 6, 5, 9, 4, 6, 5, 0x0D, 4, 6, 5, 0x0A, 4, 6, 5, 0x11,
                    4, 6, 5, 9, 4, 6, 5, 0x0E, 4, 6, 5, 0x0A, 4, 6, 5, 0x15,
                    4, 6, 5, 9, 4, 6, 5, 0x0D, 4, 6, 5, 0x0A, 4, 6, 5, 0x12,
                    4, 6, 5, 9, 4, 6, 5, 0x0E, 4, 6, 5, 0x0A, 4, 6, 5, 0x1A,
                    4, 6, 5, 9, 4, 6, 5, 0x0D, 4, 6, 5, 0x0A, 4, 6, 5, 0x11,
                    4, 6, 5, 9, 4, 6, 5, 0x0E, 4, 6, 5, 0x0A, 4, 6, 5, 0x16,
                    4, 6, 5, 9, 4, 6, 5, 0x0D, 4, 6, 5, 0x0A, 4, 6, 5, 0x12,
                    4, 6, 5, 9, 4, 6, 5, 0x0E, 4, 6, 5, 0x0A, 4, 6, 5, 2,
                    4, 0x0B, 7, 0x0F, 4, 0x0C, 8, 0x13, 4, 0x0B, 7, 0x10, 4, 0x0C,
                    8, 0x17, 4, 0x0B, 7, 0x0F, 4, 0x0C, 8, 0x14, 4, 0x0B, 7, 0x10,
                    4, 0x0C, 8, 0x1B, 4, 0x0B, 7, 0x0F, 4, 0x0C, 8, 0x13, 4, 0x0B,
                    7, 0x10, 4, 0x0C, 8, 0x18, 4, 0x0B, 7, 0x0F, 4, 0x0C, 8, 0x14,
                    4, 0x0B, 7, 0x10, 4, 0x0C, 8, 1, 4, 0x0B, 7, 0x0F, 4, 0x0C,
                    8, 0x13, 4, 0x0B, 7, 0x10, 4, 0x0C, 8, 0x17, 4, 0x0B, 7, 0x0F,
                    4, 0x0C, 8, 0x14, 4, 0x0B, 7, 0x10, 4, 0x0C, 8, 0x1C, 4, 0x0B,
                    7, 0x0F, 4, 0x0C, 8, 0x13, 4, 0x0B, 7, 0x10, 4, 0x0C, 8, 0x18,
                    4, 0x0B, 7, 0x0F, 4, 0x0C, 8, 0x14, 4, 0x0B, 7, 0x10, 4, 0x0C,
                    8, 3, 4, 0x0B, 7, 0x0F, 4, 0x0C, 8, 0x13, 4, 0x0B, 7, 0x10,
                    4, 0x0C, 8, 0x17, 4, 0x0B, 7, 0x0F, 4, 0x0C, 8, 0x14, 4, 0x0B,
                    7, 0x10, 4, 0x0C, 8, 0x1B, 4, 0x0B, 7, 0x0F, 4, 0x0C, 8, 0x13,
                    4, 0x0B, 7, 0x10, 4, 0x0C, 8, 0x18, 4, 0x0B, 7, 0x0F, 4, 0x0C,
                    8, 0x14, 4, 0x0B, 7, 0x10, 4, 0x0C, 8, 1, 4, 0x0B, 7, 0x0F,
                    4, 0x0C, 8, 0x13, 4, 0x0B, 7, 0x10, 4, 0x0C, 8, 0x17, 4, 0x0B,
                    7, 0x0F, 4, 0x0C, 8, 0x14, 4, 0x0B, 7, 0x10, 4, 0x0C, 8, 0x1C,
                    4, 0x0B, 7, 0x0F, 4, 0x0C, 8, 0x13, 4, 0x0B, 7, 0x10, 4, 0x0C,
                    8, 0x18, 4, 0x0B, 7, 0x0F, 4, 0x0C, 8, 0x14, 4, 0x0B, 7, 0x10,
                    4, 0x0C, 8, 3
                };

        struct LookupTableData
        {
            public LookupTableData(uint HighBits, uint SkipBits, float Float)
            {
                this.HighBits = HighBits;
                this.SkipBits = SkipBits;
                this.Float = Float;
            }
            public uint HighBits;
            public uint SkipBits;
            public float Float;
        }
        LookupTableData[] LookupTable = new LookupTableData[]{
                new LookupTableData(1, 8, 0.0f),
                new LookupTableData(1, 7, 0.0f),
                new LookupTableData(0, 8, 0.0f),
                new LookupTableData(0, 7, 0.0f),
                new LookupTableData(0, 2, 0.0f),
                new LookupTableData(0, 2, -1.0f),
                new LookupTableData(0, 2, 1.0f),
                new LookupTableData(0, 3, -1.0f),
                new LookupTableData(0, 3, 1.0f),
                new LookupTableData(1, 4, -2.0f),
                new LookupTableData(1, 4, 2.0f),
                new LookupTableData(1, 3, -2.0f),
                new LookupTableData(1, 3, 2.0f),
                new LookupTableData(1, 5, -3.0f),
                new LookupTableData(1, 5, 3.0f),
                new LookupTableData(1, 4, -3.0f),
                new LookupTableData(1, 4, 3.0f),
                new LookupTableData(1, 6, -4.0f),
                new LookupTableData(1, 6, 4.0f),
                new LookupTableData(1, 5, -4.0f),
                new LookupTableData(1, 5, 4.0f),
                new LookupTableData(1, 7, -5.0f),
                new LookupTableData(1, 7, 5.0f),
                new LookupTableData(1, 6, -5.0f),
                new LookupTableData(1, 6, 5.0f),
                new LookupTableData(1, 8, -6.0f),
                new LookupTableData(1, 8, 6.0f),
                new LookupTableData(1, 7, -6.0f),
                new LookupTableData(1, 7, 6.0f),
            };

        float[] FloatLookupTable = new float[]{
                0.0f, -9.9677598e-1f, -9.90327e-1f, -9.8387903e-1f, -9.77431e-1f,
                -9.7098202e-1f, -9.6453398e-1f, -9.58085e-1f, -9.5163703e-1f,
                -9.3075401e-1f, -9.0495998e-1f, -8.7916702e-1f, -8.5337299e-1f,
                -8.2757902e-1f, -8.0178601e-1f, -7.7599198e-1f, -7.5019801e-1f,
                -7.2440499e-1f, -6.9861102e-1f, -6.7063498e-1f, -6.19048e-1f,
                -5.6746e-1f, -5.1587301e-1f, -4.64286e-1f, -4.12698e-1f,
                -3.6111099e-1f, -3.09524e-1f, -2.5793701e-1f, -2.06349e-1f,
                -1.54762e-1f, -1.03175e-1f, -5.1587e-2f, 0.0f, 5.1587e-2f,
                1.03175e-1f, 1.54762e-1f, 2.06349e-1f, 2.5793701e-1f, 3.09524e-1f,
                3.6111099e-1f, 4.12698e-1f, 4.64286e-1f, 5.1587301e-1f,
                5.6746e-1f, 6.19048e-1f, 6.7063498e-1f, 6.9861102e-1f, 7.2440499e-1f,
                7.5019801e-1f, 7.7599198e-1f, 8.0178601e-1f, 8.2757902e-1f,
                8.5337299e-1f, 8.7916702e-1f, 9.0495998e-1f, 9.3075401e-1f,
                9.5163703e-1f, 9.58085e-1f, 9.6453398e-1f, 9.7098202e-1f,
                9.77431e-1f, 9.8387903e-1f, 9.90327e-1f, 9.9677598e-1f
            };

        uint GetBits(M10DecoderPrivate decoderStruct, uint Count)
        {
            var result = decoderStruct.CurrentBits & BitmaskLookupTable[Count];
            decoderStruct.BitCount -= Count;
            decoderStruct.CurrentBits >>= (int)Count;

            if (decoderStruct.BitCount < 8)
            {
                var value = (decoderStruct.CompressedData.BaseStream.Position == decoderStruct.CompressedData.BaseStream.Length) ? 0 : decoderStruct.CompressedData.ReadByte();
                var newBits = (uint)(value << (byte)decoderStruct.BitCount);
                decoderStruct.CurrentBits = newBits | decoderStruct.CurrentBits;
                decoderStruct.BitCount += 8;
            }
            return result;
        }

        void SkipBits(M10DecoderPrivate decoderStruct, uint Count)
        {
            decoderStruct.BitCount -= Count;
            decoderStruct.CurrentBits >>= (int)Count;

            if (decoderStruct.BitCount < 8)
            {
                var value = (decoderStruct.CompressedData.BaseStream.Position == decoderStruct.CompressedData.BaseStream.Length) ? 0 : decoderStruct.CompressedData.ReadByte();
                var newBits = (uint)(value << (byte)decoderStruct.BitCount);
                decoderStruct.CurrentBits = newBits | decoderStruct.CurrentBits;
                decoderStruct.BitCount += 8;
            }
        }

        void FunctionOne(float[] buffer, int bufferIndex)
        {
            var currentBufferPtr = bufferIndex + 5;

            for (var i = 0; i < 54; i++)
            {
                var a = buffer[currentBufferPtr - 8] + buffer[currentBufferPtr - 2];
                var b = buffer[currentBufferPtr - 10] + buffer[currentBufferPtr];
                var c = buffer[currentBufferPtr - 6] + buffer[currentBufferPtr - 4];

                buffer[currentBufferPtr - 5] = (float)(a * -0.11459156 + b * 0.01803268 + c * 0.59738597);
                currentBufferPtr += 2;
            }
        }

        void FunctionTwo(float[] decoderStructTable1, float[] Arg2)
        {
            var table = new float[24];

            for (var i = 0; i < 11; i++)
            {
                table[11 - i] = decoderStructTable1[10 - i];
            }

            table[0] = 1.0f;

            for (var i = 0; i < 12; i++)
            {
                var previous = -table[11] * decoderStructTable1[11];

                for (var counter = 0; counter < 11; counter++)
                {
                    var a = table[10 - counter];
                    var b = decoderStructTable1[10 - counter];

                    previous -= a * b;
                    table[11 - counter] = previous * b + a;
                }

                table[0] = previous;
                table[i + 12] = previous;

                if (i > 0)
                {
                    for (uint j = 0; j < i; j++)
                    {
                        previous -= table[11 + i - j] * Arg2[j];
                    }
                }

                Arg2[i] = previous;
            }
        }

        void FunctionThree(M10DecoderPrivate decoderStruct, int flag, float[] output, int outputIndex, uint count)
        {
            if (flag != 0)
            {
                var index = 0u;
                var highBits = 0u;

                do
                {
                    var bits = decoderStruct.CurrentBits & 0xFF;
                    var lookedUpValue = ByteLookupTable[(highBits << 8) + bits];
                    highBits = LookupTable[lookedUpValue].HighBits;

                    SkipBits(decoderStruct, LookupTable[lookedUpValue].SkipBits);

                    if (lookedUpValue > 3)
                    {
                        output[outputIndex + index] = LookupTable[lookedUpValue].Float;
                        index += count;
                    }
                    else if (lookedUpValue > 1)
                    {
                        bits = GetBits(decoderStruct, 6) + 7;

                        if (bits * count + index > 108)
                        {
                            bits = (108 - index) / count;
                        }

                        if (bits > 0)
                        {
                            for (var i = 0; i < bits; i++)
                            {
                                output[outputIndex + index + count * i] = 0;
                            }
                            index += bits * count;
                        }
                    }
                    else
                    {
                        var bitsCount = 7;

                        while (GetBits(decoderStruct, 1) == 1)
                        {
                            bitsCount++;
                        }

                        if (GetBits(decoderStruct, 1) != 0)
                        {
                            output[outputIndex + index] = bitsCount;
                        }
                        else
                        {
                            output[outputIndex + index] = -bitsCount;
                        }

                        index += count;
                    }
                } while (index < 108);
            }
            else
            {
                var index = 0u;

                do
                {
                    switch (decoderStruct.CurrentBits & 0x3)
                    {
                        case 1:
                            output[outputIndex + index] = -2.0f;
                            SkipBits(decoderStruct, 2);
                            break;
                        case 3:
                            output[outputIndex + index] = 2.0f;
                            SkipBits(decoderStruct, 2);
                            break;
                        case 2:
                        case 0:
                            output[outputIndex + index] = 0f;
                            SkipBits(decoderStruct, 1);
                            break;
                        default:
                            break;
                    }
                    index += count;
                } while (index < 108);
            }
        }

        void FunctionFour(M10DecoderPrivate decoderStruct, uint index, uint count)
        {
            var buffer = new float[12];
            FunctionTwo(decoderStruct.Table1, buffer);
            var sampleBufferPtr = index;

            for (uint i = 0; i < count; i++)
            {
                for (uint k = 0; k < 12; k++)
                {
                    var sum = 0.0;
                    for (var j = 0; j < 12; j++)
                    {
                        sum += decoderStruct.Table2[j] * buffer[(j + k) % 12];
                    }

                    double result = decoderStruct.SampleBuffer[sampleBufferPtr + k] + sum;
                    decoderStruct.Table2[11 - k] = (float)result;
                    decoderStruct.SampleBuffer[sampleBufferPtr + k] = (float)result;
                }
                sampleBufferPtr += 12;
            }
        }

        class M10DecoderPrivate
        {
            public BinaryReader CompressedData;
            public uint CurrentBits = 0;
            public uint BitCount = 0;
            public uint FirstBit = 0;
            public uint Second4Bits = 0;
            public float[] FloatTable = new float[64];
            public float[] Table1 = new float[12];
            public float[] Table2 = new float[12];
            public float[] BigTable = new float[756];
            public float[] SampleBuffer = new float[432];
        };


        [StructLayout(LayoutKind.Explicit)]
        public struct FloatInt
        {
            [FieldOffset(0)] public float Float;
            [FieldOffset(0)] public int Int;
        }

        public void Save(string path)
        {
            File.WriteAllBytes(Path.Join(path, FileName.Replace(".m10", ".wav")), data);
        }
    }
}