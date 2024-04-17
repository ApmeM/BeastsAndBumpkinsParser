namespace MyONez.PipelineImporter.Tiled.ImportModels
{
    using System.Xml.Serialization;

    public enum TmxOrientation
    {
        [XmlEnum( Name = "orthogonal" )]
        Orthogonal = 1,
        [XmlEnum( Name = "isometric" )] 
        Isometric = 2,
        [XmlEnum( Name = "staggered" )] 
        Staggered = 3
    }
}