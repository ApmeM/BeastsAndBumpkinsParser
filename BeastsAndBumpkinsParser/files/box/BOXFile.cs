using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BeastsAndBumpkinsParser
{
    public class BOXFile : IFile
    {
        public string FileName;

        public readonly List<IFile> result = new List<IFile>();

        public BOXFile(string fileName, byte[] data)
        {
            FileName = fileName;

            using var stream = new MemoryStream(data);
            using var binr = new BinaryReader(stream, Encoding.ASCII);

            if (Encoding.ASCII.GetString(binr.ReadBytes(3)) != "BOX")
            {
                throw new Exception("BOX file is corrupted. Cant read header.");
            }

            binr.BaseStream.Seek(5, SeekOrigin.Current);

            while (binr.PeekChar() != -1)
            {
                var entryName = Utils.TrimFromZero(new string(binr.ReadChars(256))).ToLower();
                var entryPath = Utils.TrimFromZero(new string(binr.ReadChars(256)));
                var timeYear = binr.ReadUInt16();
                var timeMonth = binr.ReadUInt16();
                var timeDOW = binr.ReadUInt16();
                var timeDay = binr.ReadUInt16();
                var timeHour = binr.ReadUInt16();
                var timeMinute = binr.ReadUInt16();
                var timeSecond = binr.ReadUInt16();
                var timeMills = binr.ReadUInt16();
                var size = binr.ReadInt32();
                var entryData = binr.ReadBytes(size);

                try
                {
                    if (entryName.EndsWith(".MFB", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.Add(new MFBFile(entryName, entryData));
                    }
                    else
                    if (entryName.EndsWith(".MIS", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.Add(new MAPFile(entryName, entryData));
                    }
                    else
                    if (entryName.EndsWith(".SAV", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.Add(new MAPFile(entryName, entryData));
                    }
                    else
                    if (entryName.EndsWith(".BOX", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.Add(new BOXFile(entryName, entryData));
                    }
                    else
                    if (entryName.EndsWith(".M10", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.Add(new M10File(entryName, entryData));
                    }
                    else
                    {
                        result.Add(new BinaryFile(entryName, entryData));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {entryName} {ex}");
                }
            }
        }

        public void Save(string path)
        {
            var resultDirectory = Path.Join(path, Path.GetFileNameWithoutExtension(this.FileName));
            if (Directory.Exists(resultDirectory))
            {
                Directory.Delete(resultDirectory, true);
            }
            Directory.CreateDirectory(resultDirectory);

            foreach (var entry in result)
            {
                entry.Save(resultDirectory);
            }
        }
    }
}