namespace MyONez.PipelineImporter.Tiled.ImportModels
{
    using System.Collections.Generic;
    using System.Xml.Serialization;

    public class TmxObjectGroup
    {
        [XmlAttribute( AttributeName = "id" )]
        public int Id;

        [XmlAttribute( AttributeName = "offsetx" )]
        public float OffsetX;

        [XmlAttribute( AttributeName = "offsety" )]
        public float OffsetY;

        [XmlAttribute( AttributeName = "name" )]
        public string Name;

        [XmlAttribute( AttributeName = "color" )]
        public string Color;

        [XmlAttribute( AttributeName = "opacity" )]
        public float Opacity = 1f;

        [XmlAttribute( AttributeName = "visible" )]
        public bool Visible = true;

        [XmlArray( "properties" )]
        [XmlArrayItem( "property" )]
        public List<TmxProperty> Properties;

        [XmlElement( ElementName = "object" )]
        public List<TmxObject> Objects = new List<TmxObject>();
    }
}