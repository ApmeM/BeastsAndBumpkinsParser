namespace MyONez.PipelineImporter.Tiled.ImportModels
{
    using System.Collections.Generic;
    using System.Xml.Serialization;

    public class TmxTileSetTile
    {
        [XmlAttribute( AttributeName = "id" )]
        public int Id;

        [XmlElement( ElementName = "terrain" )]
        public TmxTerrain Terrain;

        [XmlAttribute( AttributeName = "probability" )]
        public float Probability = 1f;

        [XmlElement( ElementName = "image" )]
        public TmxImage Image;

        [XmlElement( ElementName = "objectgroup" )]
        public List<TmxObjectGroup> ObjectGroups;

        [XmlArray( "properties" )]
        [XmlArrayItem( "property" )]
        public List<TmxProperty> Properties = new List<TmxProperty>();

        [XmlArray( "animation" )]
        [XmlArrayItem( "frame" )]
        public List<TmxTilesetTileAnimationFrame> AnimationFrames;

        /// <summary>
        /// source Rectangle for tilesets that use the collection of images
        /// </summary>
        public TmxRectangle SourceRect;
    }
}