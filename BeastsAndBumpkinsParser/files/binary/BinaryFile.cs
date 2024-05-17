using System.IO;

namespace BeastsAndBumpkinsParser
{
    public class BinaryFile : IFile
    {
        private string FileName;
        private byte[] data;

        public BinaryFile(string fileName, byte[] data)
        {
            this.FileName = fileName;
            this.data = data;
        }

        public void Save(string path)
        {
            File.WriteAllBytes(Path.Join(path, FileName), data);
        }
    }
}