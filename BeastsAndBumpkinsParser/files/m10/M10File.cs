using System;
using System.IO;
using System.Linq;
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

            using var stream = new MemoryStream(data);
            using var binr = new BinaryReader(stream, Encoding.ASCII);

            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);

            this.Initialize(binr);

            short[] Samples = new short[4096];
            uint Decoded;
            uint SampleCount = 0;
            do
            {
                Decoded = this.Decode(Samples, 4096);
                foreach (var i in Samples.Take((int)Decoded))
                {
                    writer.Write(i);
                }
                SampleCount += Decoded;
            } while (Decoded == 4096);

            writer.BaseStream.Seek(0, SeekOrigin.Begin);
            WriteWaveHeader(writer, 22050, 16, 1, SampleCount);

            writer.BaseStream.Seek(0, SeekOrigin.Begin);
            this.data = output.ToArray();
        }

        void WriteWaveHeader(BinaryWriter Output, uint SampleRate, ushort BitsPerSample, ushort Channels, uint NumberSamples)
        {
            // Write some RIFF information
            Output.Write(new[] { 'R', 'I', 'F', 'F' });
            Output.Write((uint)(NumberSamples * (BitsPerSample / 8) + 36));
            Output.Write(new[] { 'W', 'A', 'V', 'E' });
            Output.Write(new[] { 'f', 'm', 't', ' ' });
            Output.Write((uint)16);

            // Write the format chunk
            Output.Write((ushort)1);
            Output.Write((ushort)Channels);
            Output.Write((uint)SampleRate);
            Output.Write((uint)BitsPerSample / 8 * SampleRate * Channels);
            Output.Write((ushort)(BitsPerSample / 8 * Channels));
            Output.Write((ushort)BitsPerSample);

            // Write the data information
            Output.Write(new[] { 'd', 'a', 't', 'a' });
            Output.Write((uint)(NumberSamples * (BitsPerSample / 8)));
        }

        SM10DecoderPrivate m_Input;
        uint m_SampleBufferOffset;
        uint m_SamplesDecodedSoFar;
        uint m_TotalSampleCount;

        public void Initialize(BinaryReader Input)
        {
            // Read the PT header
            if (!ParsePTHeader(Input))
            {
                return;
            }

            // Initialize the decoder
            m_Input = new SM10DecoderPrivate();
            try
            {
                InitializeDecoder(m_Input, Input);
            }
            catch (Exception ex)
            {
                Console.WriteLine(this.FileName);
                Console.WriteLine($"The decoder could not be initialized (bug in program): {ex}");
            }
            m_SampleBufferOffset = 432;
            m_SamplesDecodedSoFar = 0;
        }

        bool ParsePTHeader(BinaryReader Input)
        {
            // Signature
            if (Encoding.ASCII.GetString(Input.ReadBytes(2)) != "PT")
            {
                Console.WriteLine("Missing PT signature.");
                return false;
            }

            Input.ReadBytes(2);

            // Data offset
            var DataOffset = Input.ReadUInt32();

            // Read in the header (code borrowed from Valery V. Anisimovsky (samael@avn.mccme.ru) )
            var bInHeader = true;
            while (bInHeader)
            {
                var Byte = Input.ReadByte();
                switch (Byte) // parse header code
                {
                    case 0xFF: // end of header
                        bInHeader = false;
                        break;
                    case 0xFE: // skip
                    case 0xFC: // skip
                        break;
                    case 0xFD: // subheader starts...
                        var bInSubHeader = true;
                        while (bInSubHeader)
                        {
                            Byte = Input.ReadByte();
                            switch (Byte) // parse subheader code
                            {
                                case 0x83:
                                    Byte = Input.ReadByte();
                                    var CompressionType = ReadBytes(Input, Byte);
                                    break;
                                case 0x85:
                                    Byte = Input.ReadByte();
                                    m_TotalSampleCount = ReadBytes(Input, Byte);
                                    break;
                                case 0xFF:
                                    break;
                                case 0x8A: // end of subheader
                                    bInSubHeader = false;
                                    Byte = Input.ReadByte();
                                    Input.BaseStream.Seek(Byte, SeekOrigin.Current);
                                    break;
                                default: // ???
                                    Byte = Input.ReadByte();
                                    Input.BaseStream.Seek(Byte, SeekOrigin.Current);
                                    break;
                            }
                        }
                        break;
                    default:
                        Byte = Input.ReadByte();
                        if (Byte == 0xFF)
                        {
                            Input.ReadBytes(4);
                        }
                        Input.ReadByte();
                        break;
                }
            }

            // Seek to the data offset
            Input.BaseStream.Seek(DataOffset, SeekOrigin.Begin);
            return true;
        }

        static uint ReadBytes(BinaryReader Input, byte Count)
        {
            byte i;
            byte Byte;
            uint Result;

            Result = 0;
            for (i = 0; i < Count; i++)
            {
                Byte = Input.ReadByte();
                Result <<= 8;
                Result |= Byte;
            }
            return Result;
        }

        void InitializeDecoder(SM10DecoderPrivate decoderStruct, BinaryReader Input)
        {
            decoderStruct.CurrentBits = Input.ReadByte();
            decoderStruct.CompressedData = Input;
            decoderStruct.BitCount = 8;
            decoderStruct.FirstBit = GetBits(decoderStruct, 1);
            decoderStruct.Second4Bits = 32 - GetBits(decoderStruct, 4);
            decoderStruct.FloatTable[0] = (float)(GetBits(decoderStruct, 4) + 1) * 8.0f;

            double AFloat = 1.04 + GetBits(decoderStruct, 6) * 0.001;
            for (uint i = 0; i < 63; i++)
            {
                decoderStruct.FloatTable[i + 1] = (float)(decoderStruct.FloatTable[i] * AFloat);
            }
        }

        public uint Decode(short[] OutputBuffer, uint SampleCount)
        {
            if (m_TotalSampleCount == 0)
            {
                return 0;
            }

            for (uint i = 0; i < SampleCount; i++)
            {
                var Sample = new mtFloatInt
                {
                    Int = 0x4B400000
                };

                if (m_SampleBufferOffset >= 432)
                {
                    try
                    {
                        DecodeBlock(m_Input);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(this.FileName);
                        Console.WriteLine($"The decoder encountered a problem (bug in program). {ex}");
                        return i;
                    }
                    m_SampleBufferOffset = 0;
                }

                Sample.Float += m_Input.SampleBuffer[m_SampleBufferOffset];
                m_SampleBufferOffset++;
                m_SamplesDecodedSoFar++;

                var Clipped = Sample.Int & 0x1FFFF;
                if (Clipped > 0x7FFF && Clipped < 0x18000)
                {
                    if (Clipped >= 0x10000)
                    {
                        Clipped = 0x8000;
                    }
                    else
                    {
                        Clipped = 0x7FFF;
                    }
                }

                OutputBuffer[i] = (short)Clipped;

                if (m_SamplesDecodedSoFar >= m_TotalSampleCount)
                {
                    return i + 1;
                }
            }
            return SampleCount;
        }

        void DecodeBlock(SM10DecoderPrivate decoderStruct)
        {
            float[] TableA = new float[12];
            float[] TableB = new float[118];

            var Bits = GetBits(decoderStruct, 6);

            var Flag = Bits < decoderStruct.Second4Bits ? 1 : 0;

            TableA[0] = (FloatLookupTable[Bits] - decoderStruct.Table1[0]) * 0.25f;

            for (uint i = 1; i < 4; i++)
            {
                Bits = GetBits(decoderStruct, 6);

                TableA[i] = (FloatLookupTable[Bits] - decoderStruct.Table1[i]) * 0.25f;
            }

            for (uint i = 4; i < 12; i++)
            {
                Bits = GetBits(decoderStruct, 5);

                TableA[i] = (FloatLookupTable[Bits + 16] - decoderStruct.Table1[i]) * 0.25f;
            }

            var CurSampleBufPtr = 0;

            for (uint i = 216; i < 648; i += 108)
            {
                var BigTableIndex = i - GetBits(decoderStruct, 8);

                var SomeFloat = GetBits(decoderStruct, 4) * 2.0f / 30.0f;
                var SomeOtherFloat = decoderStruct.FloatTable[GetBits(decoderStruct, 6)];

                if (decoderStruct.FirstBit == 0)
                {
                    FunctionThree(decoderStruct, Flag, TableB, 5, 1);
                }
                else
                {
                    var IndexAdjust = GetBits(decoderStruct, 1);
                    Bits = GetBits(decoderStruct, 1);

                    FunctionThree(decoderStruct, Flag, TableB, 5 + (int)IndexAdjust, 2);

                    if (Bits != 0)
                    {
                        for (uint j = 0; j < 108; j += 2)
                        {
                            TableB[j + 6 - IndexAdjust] = 0;
                        }
                    }
                    else
                    {
                        for (uint j = 0; j < 5; j++)
                        {
                            TableB[j] = 0;
                            TableB[j + 113] = 0;
                        }

                        FunctionOne(TableB, 6 - (int)IndexAdjust);
                        SomeOtherFloat *= 0.5f;
                    }
                }

                for (uint j = 0; j < 108; j++)
                {
                    var a = SomeOtherFloat * TableB[j + 5];
                    var b = SomeFloat * decoderStruct.BigTable[BigTableIndex + j];
                    decoderStruct.SampleBuffer[CurSampleBufPtr] = a + b;
                    CurSampleBufPtr++;
                }
            }

            for (uint i = 0; i < 324; i++)
            {
                decoderStruct.BigTable[i] = decoderStruct.SampleBuffer[i + 108];
            }

            for (uint i = 0; i < 12; i++)
            {
                decoderStruct.Table1[i] += TableA[i];
            }
            FunctionFour(decoderStruct, 0, 1);

            for (uint i = 0; i < 12; i++)
            {
                decoderStruct.Table1[i] += TableA[i];
            }
            FunctionFour(decoderStruct, 12, 1);

            for (uint i = 0; i < 12; i++)
            {
                decoderStruct.Table1[i] += TableA[i];
            }
            FunctionFour(decoderStruct, 24, 1);

            for (uint i = 0; i < 12; i++)
            {
                decoderStruct.Table1[i] += TableA[i];
            }
            FunctionFour(decoderStruct, 36, 33);
            return;
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

        uint GetBits(SM10DecoderPrivate decoderStruct, uint Count)
        {
            uint Result;
            Result = decoderStruct.CurrentBits & BitmaskLookupTable[Count];
            decoderStruct.BitCount -= Count;
            decoderStruct.CurrentBits >>= (int)Count;

            if (decoderStruct.BitCount < 8)
            {
                var NewBits = (uint)(decoderStruct.CompressedData.ReadByte() << (byte)decoderStruct.BitCount);
                decoderStruct.CurrentBits = NewBits | decoderStruct.CurrentBits;
                decoderStruct.BitCount += 8;
            }
            return Result;
        }

        void SkipBits(SM10DecoderPrivate decoderStruct, uint Count)
        {
            decoderStruct.BitCount -= Count;
            decoderStruct.CurrentBits >>= (int)Count;

            if (decoderStruct.BitCount < 8)
            {
                var NewBits = (uint)(decoderStruct.CompressedData.ReadByte() << (byte)decoderStruct.BitCount);
                decoderStruct.CurrentBits = NewBits | decoderStruct.CurrentBits;
                decoderStruct.BitCount += 8;
            }
            return;
        }

        void FunctionOne(float[] Buffer, int index)
        {
            int CurrentPtr = index + 5;

            for (uint i = 0; i < 54; i++)
            {
                double a = Buffer[CurrentPtr - 8] + Buffer[CurrentPtr - 2];
                double b = Buffer[CurrentPtr - 10] + Buffer[CurrentPtr];
                double c = Buffer[CurrentPtr - 6] + Buffer[CurrentPtr - 4];

                Buffer[CurrentPtr - 5] = (float)(a * -0.11459156 + b * 0.01803268 + c * 0.59738597);
                CurrentPtr += 2;
            }
        }

        void FunctionTwo(float[] DecoderStructTable1, float[] Arg2)
        {
            float[] Table = new float[24];

            for (byte i = 0; i < 11; i++)
            {
                Table[11 - i] = DecoderStructTable1[10 - i];
            }

            Table[0] = 1.0f;

            for (uint i = 0; i < 12; i++)
            {
                double Previous;
                Previous = -Table[11] * DecoderStructTable1[11];

                for (uint CounterC = 0; CounterC < 11; CounterC++)
                {
                    float PtrA = Table[10 - CounterC];
                    float PtrB = DecoderStructTable1[10 - CounterC];

                    Previous -= PtrA * PtrB;
                    Table[11 - CounterC] = (float)Previous * PtrB + PtrA;
                }

                Table[0] = (float)Previous;
                Table[i + 12] = (float)Previous;

                if (i > 0)
                {
                    uint CounterA = i;
                    uint CounterB = i;

                    for (uint j = 0; j < i; j++)
                    {
                        Previous -= Table[11 + i - j] * Arg2[j];
                    }
                }

                Arg2[i] = (float)Previous;
            }
        }

        void FunctionThree(SM10DecoderPrivate decoderStruct, int Flag, float[] Out, int OutIndex, uint CountInt)
        {
            if (Flag != 0)
            {
                uint Index = 0;
                uint HighBits = 0;

                do
                {
                    var Bits = decoderStruct.CurrentBits & 0xFF;
                    var LookedUpValue = ByteLookupTable[(HighBits << 8) + Bits];
                    HighBits = LookupTable[LookedUpValue].HighBits;

                    SkipBits(decoderStruct, LookupTable[LookedUpValue].SkipBits);

                    if (LookedUpValue > 3)
                    {
                        Out[OutIndex + Index] = LookupTable[LookedUpValue].Float;
                        Index += CountInt;
                    }
                    else if (LookedUpValue > 1)
                    {
                        var Bits2 = GetBits(decoderStruct, 6) + 7;

                        if (Bits2 * CountInt + Index > 108)
                        {
                            Bits2 = (108 - Index) / CountInt;
                        }

                        if (Bits2 > 0)
                        {
                            var Ptr = Index;
                            Index += Bits2 * CountInt;

                            for (uint i = 0; i < Bits2; i++)
                            {
                                Out[OutIndex + Ptr] = 0;
                                Ptr += CountInt;
                            }
                        }
                    }
                    else
                    {
                        int Count = 7;

                        while (GetBits(decoderStruct, 1) == 1)
                        {
                            Count++;
                        }

                        if (GetBits(decoderStruct, 1) != 0)
                        {
                            Out[OutIndex + Index] = Count;
                        }
                        else
                        {
                            Out[OutIndex + Index] = -Count;
                        }

                        Index += CountInt;
                    }
                } while (Index < 108);
            }
            else
            {
                uint Index = 0;

                do
                {
                    switch (decoderStruct.CurrentBits & 0x3)
                    {
                        case 1:
                            Out[OutIndex + Index] = -2.0f;
                            SkipBits(decoderStruct, 2);
                            break;
                        case 3:
                            Out[OutIndex + Index] = 2.0f;
                            SkipBits(decoderStruct, 2);
                            break;
                        case 2:
                        case 0:
                            Out[OutIndex + Index] = 0f;
                            SkipBits(decoderStruct, 1);
                            break;
                        default:
                            break;
                    }
                    Index += CountInt;
                } while (Index < 108);
            }
            return;
        }

        void FunctionFour(SM10DecoderPrivate decoderStruct, uint Index, uint Count)
        {
            float[] Buffer = new float[12];
            FunctionTwo(decoderStruct.Table1, Buffer);
            var SampleBufferPtr = Index;

            for (uint i = 0; i < Count; i++)
            {
                for (uint k = 0; k < 12; k++)
                {
                    double Summation = 0.0;
                    for (uint j = 0; j < 12; j++)
                    {
                        Summation += decoderStruct.Table2[j] * Buffer[(j + k) % 12];
                    }

                    double Result = decoderStruct.SampleBuffer[SampleBufferPtr + k] + Summation;
                    decoderStruct.Table2[11 - k] = (float)Result;
                    decoderStruct.SampleBuffer[SampleBufferPtr + k] = (float)Result;
                }
                SampleBufferPtr += 12;
            }
        }

        class SM10DecoderPrivate
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
        public struct mtFloatInt
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