namespace MyONez.PipelineImporter.Tiled.ImportModels
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Serialization;

    public class TmxData
    {
        [XmlAttribute(AttributeName = "encoding")]
        public string Encoding;

        [XmlAttribute(AttributeName = "compression")]
        public string Compression;

        [XmlIgnore]
        public List<TmxDataTile> Tiles;

        [XmlText]
        public string Value
        {
            get
            {
                return "\n" + string.Join(",", Tiles.Select(a => a.Gid)) + "\n";
            }
            set
            {

            }
        }
    }
}