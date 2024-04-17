namespace MyONez.PipelineImporter.Tiled.ImportModels
{
    using System.Xml.Serialization;

    [XmlRoot( ElementName = "frame" )]
    public class TmxTilesetTileAnimationFrame
    {
        [XmlAttribute( AttributeName = "tileid" )]
        public int TileId;

        [XmlAttribute( AttributeName = "duration" )]
        public float Duration;
    }
}

