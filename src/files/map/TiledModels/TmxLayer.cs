namespace MyONez.PipelineImporter.Tiled.ImportModels
{
    using System.Collections.Generic;
    using System.Xml.Serialization;

    [XmlInclude( typeof( TmxTileLayer ) )]
    [XmlInclude( typeof( TmxImageLayer ) )]
    public abstract class TmxLayer
    {

        [XmlAttribute( AttributeName = "id" )]
        public int Id;

        [XmlAttribute( AttributeName = "offsetx" )]
        public float OffsetX;

        [XmlAttribute( AttributeName = "offsety" )]
        public float OffsetY;

        [XmlAttribute( AttributeName = "name" )]
        public string Name;

        [XmlAttribute( AttributeName = "opacity" )]
        public float Opacity = 1f;

        [XmlIgnore]
        public bool Visible
        {
            get
            {
                return VisibleInt == 1;
            }
            set
            {
                VisibleInt = value ? 1 : 0;
            }
        }

        [XmlAttribute(AttributeName = "visible")]
        public int VisibleInt = 1;

        [XmlArray( "properties" )]
        [XmlArrayItem( "property" )]
        public List<TmxProperty> Properties;
    }
}