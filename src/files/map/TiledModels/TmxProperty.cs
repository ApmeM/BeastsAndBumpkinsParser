namespace MyONez.PipelineImporter.Tiled.ImportModels
{
    using System.Xml.Serialization;

    public class TmxProperty
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name;

        [XmlAttribute(AttributeName = "value")]
        public string Value;
    }
}