namespace MyONez.PipelineImporter.Tiled.ImportModels
{
    using System.Collections.Generic;
    using System.Xml.Serialization;

    [XmlRoot( ElementName = "tileset" )]
    public class TmxTileSet
    {
        [XmlAttribute( AttributeName = "firstgid" )]
        public int FirstGid;

        [XmlAttribute( AttributeName = "source" )]
        public string Source;

        [XmlAttribute( AttributeName = "name" )]
        public string Name;

        [XmlAttribute( AttributeName = "tilewidth" )]
        public int TileWidth;

        [XmlAttribute( AttributeName = "tileheight" )]
        public int TileHeight;

        [XmlAttribute( AttributeName = "spacing" )]
        public int Spacing;

        [XmlAttribute( AttributeName = "margin" )]
        public int Margin;

        [XmlAttribute( AttributeName = "tilecount" )]
        public int TileCount;

        [XmlAttribute( AttributeName = "columns" )]
        public int Columns;

        [XmlElement( ElementName = "tileoffset" )]
        public TmxTileOffset TileOffset;

        [XmlElement( ElementName = "tile" )]
        public List<TmxTileSetTile> Tiles;

        [XmlArray( "properties" )]
        [XmlArrayItem( "property" )]
        public List<TmxProperty> Properties;

        [XmlElement( ElementName = "image" )]
        public TmxImage Image;

        [XmlArray( "terraintypes" )]
        [XmlArrayItem( "terrain" )]
        public List<TmxTerrain> TerrainTypes;
    }
}