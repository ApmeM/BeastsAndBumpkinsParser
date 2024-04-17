using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BBData
{
    public class MFBFile : IFile
    {
        public string FileName;
        public Palettes.TypePalette palette = Palettes.TypePalette.PALETTE;


        public readonly int NumSptites;
        public readonly int Width;
        public readonly int Height;
        public readonly Point Offset;
        public readonly bool IsCompressed;
        public readonly bool IsTransparent;
        public readonly bool IsUnknown;
        public readonly List<Image> result = new List<Image>();

        public MFBFile(string fileName, byte[] data)
        {
            FileName = fileName;

            using var stream = new MemoryStream(data);
            using var binr = new BinaryReader(stream, Encoding.ASCII);

            if (Encoding.ASCII.GetString(binr.ReadBytes(3)) != "MFB")
            {
                throw new Exception("MFB file is corrupted. Cant read header.");
            }

            var version = int.Parse(new string(binr.ReadChars(3))); // 101

            if (version != 101)
            {
                throw new Exception("MFB file version is not supported.");
            }

            Width = binr.ReadInt16();
            Height = binr.ReadInt16();
            Offset = new Point(binr.ReadInt16(), binr.ReadInt16());

            var flags = binr.ReadInt16();
            IsTransparent = (flags & (byte)EntryFlags.Transparent) != 0;
            IsUnknown = (flags & (byte)EntryFlags.Unknown) != 0;
            IsCompressed = (flags & (byte)EntryFlags.Compressed) != 0;

            NumSptites = binr.ReadInt16();

            var spritesize = Width * Height;

            if (IsCompressed)
            {
                for (var i = 0; i < NumSptites; i++)
                {
                    var size = binr.ReadInt32();
                    result.Add(CreateImage(Utils.UnpackRLE(binr.ReadBytes(size), spritesize), palette));
                }
            }
            else
            {
                for (var i = 0; i < NumSptites; i++)
                {
                    result.Add(CreateImage(binr.ReadBytes(spritesize), palette));
                }
            }
        }

        private Image CreateImage(byte[] buffer, Palettes.TypePalette palette)
        {
            var p = Palettes.GetPalette(palette);
            var img = new Image<Rgba32>(Width, Height);

            for (int i = 0; i < img.Height; i++)
            {
                for (int j = 0; j < img.Width; j++)
                {
                    int curPixel = j + i * img.Width;
                    img[j, i] = new Rgba32(p[buffer[curPixel] * 4], p[buffer[curPixel] * 4 + 1], p[buffer[curPixel] * 4 + 2], p[buffer[curPixel] * 4 + 3]);
                }
            }
            return img;
        }

        public void Save(string path)
        {
            var palettes = new List<List<Image>>{
                this.result,
            };

            var result = new Image<Rgba32>(this.Width * palettes[0].Count, this.Height * palettes.Count);

            for (var j = 0; j < palettes.Count; j++)
            {
                for (int i = 0; i < palettes[j].Count; i++)
                {
                    result.Mutate(o => o.DrawImage(palettes[j][i], new Point(i * this.Width, j * this.Height), 1f));
                }
            }


            bool firstColorSet = false;
            Vector4 firstColor = default;

            if (IsTransparent)
            {
                result.Mutate(x => x.ProcessPixelRowsAsVector4(row =>
                {
                    for (int x = 0; x < row.Length; x++)
                    {
                        if (!firstColorSet)
                        {
                            firstColor = row[x];
                            firstColorSet = true;
                        }

                        if (row[x] == firstColor)
                        {
                            row[x].W = 0;
                        }
                    }
                }));
            }
            result.Save($"{path}/{this.FileName.Replace(".mfb", ".png")}", new PngEncoder());
        }

        private enum EntryFlags
        {
            Transparent = 1,
            Compressed = 2,
            Unknown = 4
        }
    }
}