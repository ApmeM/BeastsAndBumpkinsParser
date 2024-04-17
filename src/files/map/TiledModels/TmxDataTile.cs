namespace MyONez.PipelineImporter.Tiled.ImportModels
{
    using System.Xml.Serialization;

    public class TmxDataTile
    {
        [XmlAttribute( AttributeName = "gid" )]
        public uint Gid;

        [XmlAttribute(AttributeName = "flippedHorizontally")]
        public bool FlippedHorizontally;

        [XmlAttribute(AttributeName = "flippedVertically")]
        public bool FlippedVertically;

        [XmlAttribute(AttributeName = "flippedDiagonally")]
        public bool FlippedDiagonally;
    }
}
