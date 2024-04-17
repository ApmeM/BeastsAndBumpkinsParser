using System;
using System.IO;
using System.Linq;
using BBData;

namespace SpriteViewer
{
    internal static class Program
    {
        public static BOXFile VIDEO;

        public static void Main(string[] args)
        {
            var resources = "/home/vas/Downloads/Beasts_and_Bumpkins-THEiSOZONE/res/";
            var result = "../Result";

            if (args.Length > 0)
            {
                resources = args[0];
            }
            if (args.Length > 1)
            {
                result = args[1];
            }

            VIDEO = new BOXFile("VIDEO.BOX", File.ReadAllBytes(Path.Combine(resources, "VIDEO.BOX")));

            var files = Directory.GetFiles(resources)
                .Where(a => a.EndsWith(".BOX", StringComparison.InvariantCultureIgnoreCase));
            
            foreach (var file in files)
            {
                var box = new BOXFile(Path.GetFileName(file), File.ReadAllBytes(file));
                box.Save(result);
            }
        }
    }
}