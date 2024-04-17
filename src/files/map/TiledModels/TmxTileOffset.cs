namespace MyONez.PipelineImporter.Tiled.ImportModels
{
    using System.Xml.Serialization;

    [XmlRoot( ElementName = "tileoffset" )]
    public class TmxTileOffset
    {
        [XmlAttribute( AttributeName = "x" )]
        public int X;

        [XmlAttribute( AttributeName = "y" )]
        public int Y;
    }
}