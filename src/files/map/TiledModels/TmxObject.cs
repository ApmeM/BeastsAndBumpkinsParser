namespace MyONez.PipelineImporter.Tiled.ImportModels
{
    using System.Collections.Generic;
    using System.Xml.Serialization;

    public class TmxObject
    {
        [XmlAttribute(AttributeName = "id")]
        public int Id;

        [XmlAttribute(AttributeName = "name")]
        public string Name;

        [XmlAttribute(AttributeName = "type")]
        public string Type;

        [XmlAttribute(AttributeName = "x")]
        public float X;

        [XmlAttribute(AttributeName = "y")]
        public float Y;

        [XmlAttribute(AttributeName = "width")]
        public float Width;

        [XmlAttribute(AttributeName = "height")]
        public float Height;

        [XmlAttribute(AttributeName = "rotation")]
        public int Rotation;

        [XmlAttribute(AttributeName = "gid")]
        public uint Gid;

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

        [XmlElement(ElementName = "image")]
        public TmxImage Image;

        [XmlElement(ElementName = "ellipse")]
        public TmxEllipse Ellipse;

        [XmlElement(ElementName = "polygon")]
        public TmxPolygon Polygon;

        [XmlElement(ElementName = "polyline")]
        public TmxPolyLine PolyLine;

        [XmlArray("properties")]
        [XmlArrayItem("property")]
        public List<TmxProperty> Properties;
    }
}
