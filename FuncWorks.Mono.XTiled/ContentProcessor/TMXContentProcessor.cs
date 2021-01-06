using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Framework.Utilities.Deflate;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace FuncWorks.XNA.XTiled
{
    public class TMXContentProcessor
    {
        private const UInt32 FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
        private const UInt32 FLIPPED_VERTICALLY_FLAG = 0x40000000;
        private const UInt32 FLIPPED_DIAGONALLY_FLAG = 0x20000000;

        private static string _tmxResourcesFullPath;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tmxPath">Content path to the TXM file. Example; "Levels/level1.tmx".</param>
        /// <param name="tmxResourcesPath">Content path to the TXM resources (tsx, images etc). Example; "Levels/TileSets"</param>
        /// <param name="content">Microsoft.Xna.Framework.Content.ContentManager</param>
        /// <returns></returns>
        public static Map LoadTMX(string tmxPath, string tmxResourcesPath, ContentManager content)
        {
            var fileName = tmxPath.Contains("/") ? tmxPath.Split("/").Last() : tmxPath;
            _tmxResourcesFullPath = "Content/" + tmxResourcesPath;

            var doc = XDocument.Load("Content/" + tmxPath);
            doc.Document.Root.Add(new XElement("File",
                new XAttribute("name", Path.GetFileName(fileName)),
                new XAttribute("path", Path.GetDirectoryName(fileName))));

            return Process(doc, content);
        }

        private static Map Process(XDocument input, ContentManager content)
        {
            CultureInfo culture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            Map map = new Map();
            map.LoadTextures = true;
            List<Tile> mapTiles = new List<Tile>();
            Dictionary<UInt32, Int32> gid2id = new Dictionary<UInt32, Int32>();
            gid2id.Add(0, -1);

            String mapDirectory = _tmxResourcesFullPath;
            var old = input.Document.Root.Element("File").Attribute("path").Value;
            Decimal Version = Convert.ToDecimal(input.Document.Root.Attribute("version").Value);

            switch (input.Document.Root.Attribute("orientation").Value)
            {
                case "orthogonal":
                    map.Orientation = MapOrientation.Orthogonal;
                    break;

                case "isometric":
                    map.Orientation = MapOrientation.Isometric;
                    break;

                default:
                    throw new NotSupportedException("XTiled supports only orthogonal or isometric maps");
            }

            map.Width = Convert.ToInt32(input.Document.Root.Attribute("width").Value);
            map.Height = Convert.ToInt32(input.Document.Root.Attribute("height").Value);
            map.TileWidth = Convert.ToInt32(input.Document.Root.Attribute("tilewidth").Value);
            map.TileHeight = Convert.ToInt32(input.Document.Root.Attribute("tileheight").Value);
            map.Bounds = new Rectangle(0, 0, map.Width * map.TileWidth, map.Height * map.TileHeight);

            map.Properties = new Dictionary<String, Property>();
            if (input.Document.Root.Element("properties") != null)
                foreach (var pElem in input.Document.Root.Element("properties").Elements("property"))
                    map.Properties.Add(pElem.Attribute("name").Value, Property.Create(pElem.Attribute("value").Value));

            List<Tileset> tilesets = new List<Tileset>();
            foreach (var elem in input.Document.Root.Elements("tileset"))
            {
                Tileset t = new Tileset();
                XElement tElem = elem;
                UInt32 FirstGID = Convert.ToUInt32(tElem.Attribute("firstgid").Value);
                string fileRoot = mapDirectory;

                if (elem.Attribute("source") != null)
                {
                    fileRoot = Path.Combine(mapDirectory, elem.Attribute("source").Value);
                    XDocument tsx = XDocument.Load(fileRoot);
                    fileRoot = Path.GetDirectoryName(fileRoot);
                    tElem = tsx.Root;
                }

                t.Name = tElem.Attribute("name") == null ? null : tElem.Attribute("name").Value;
                t.TileWidth = tElem.Attribute("tilewidth") == null ? 0 : Convert.ToInt32(tElem.Attribute("tilewidth").Value);
                t.TileHeight = tElem.Attribute("tileheight") == null ? 0 : Convert.ToInt32(tElem.Attribute("tileheight").Value);
                t.Spacing = tElem.Attribute("spacing") == null ? 0 : Convert.ToInt32(tElem.Attribute("spacing").Value);
                t.Margin = tElem.Attribute("margin") == null ? 0 : Convert.ToInt32(tElem.Attribute("margin").Value);

                if (tElem.Element("tileoffset") != null)
                {
                    t.TileOffsetX = Convert.ToInt32(tElem.Element("tileoffset").Attribute("x").Value);
                    t.TileOffsetY = Convert.ToInt32(tElem.Element("tileoffset").Attribute("y").Value);
                }
                else
                {
                    t.TileOffsetX = 0;
                    t.TileOffsetY = 0;
                }

                if (tElem.Element("image") != null)
                {
                    XElement imgElem = tElem.Element("image");
                    var imageSource = imgElem.Attribute("source").Value;
                    imageSource = imageSource.Contains("/") ? imageSource.Split("/").ToList().Last() : imageSource;
                    t.ImageFileName = Path.Combine(fileRoot, imageSource);
                    t.ImageWidth = imgElem.Attribute("width") == null ? -1 : Convert.ToInt32(imgElem.Attribute("width").Value);
                    t.ImageHeight = imgElem.Attribute("height") == null ? -1 : Convert.ToInt32(imgElem.Attribute("height").Value);
                    t.ImageTransparentColor = null;
                    if (imgElem.Attribute("trans") != null)
                    {
                        t.ImageTransparentColor = HexColorToRGBColor("#" + imgElem.Attribute("trans").Value.TrimStart('#'));
                    }

                    var extension = Path.GetExtension(t.ImageFileName);
                    var fileName = t.ImageFileName.Replace(extension, "").Replace("Content/", "").Replace("Content\\", "");

                    if (t.ImageWidth == -1 || t.ImageHeight == -1)
                    {
                        try
                        {
                            FileStream fileStream = new FileStream(t.ImageFileName, FileMode.Open);
                            Texture2D img = content.Load<Texture2D>(fileName);
                            fileStream.Dispose();
                            t.ImageHeight = img.Height;
                            t.ImageWidth = img.Width;
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(String.Format("Image size not set for {0} and error loading file.", t.ImageFileName), ex);
                        }
                    }

                    t.Texture = content.Load<Texture2D>(fileName);
                }

                UInt32 gid = FirstGID;
                for (int y = t.Margin; y < t.ImageHeight - t.Margin; y += t.TileHeight + t.Spacing)
                {
                    if (y + t.TileHeight > t.ImageHeight - t.Margin)
                        continue;

                    for (int x = t.Margin; x < t.ImageWidth - t.Margin; x += t.TileWidth + t.Spacing)
                    {
                        if (x + t.TileWidth > t.ImageWidth - t.Margin)
                            continue;

                        Tile tile = new Tile();
                        tile.Source = new Microsoft.Xna.Framework.Rectangle(x, y, t.TileWidth, t.TileHeight);
                        tile.Origin = new Vector2(t.TileWidth / 2, t.TileHeight / 2);
                        tile.TilesetID = tilesets.Count;
                        tile.Properties = new Dictionary<String, Property>();
                        mapTiles.Add(tile);

                        gid2id[gid] = mapTiles.Count - 1;
                        gid++;
                    }
                }

                List<Terrain> terrainTypes = new List<Terrain>();
                var terrainsElem = tElem.Element("terraintypes");
                if (terrainsElem != null)
                {
                    foreach (var terrainElem in terrainsElem.Elements("terrain"))
                    {
                        var tileAttr = terrainElem.Attribute("tile")?.Value;
                        int? tile = tileAttr != null ? int.Parse(tileAttr) : (int?)null;

                        var customProperties = terrainElem.Element("properties")?.Elements("property")
                                .ToDictionary(p => p.Attribute("name").Value,
                                                   p => Property.Create(p.Attribute("value").Value))
                                               ?? new Dictionary<string, Property>();

                        terrainTypes.Add(new Terrain
                        {
                            Name = terrainElem.Attribute("name")?.Value,
                            TileID = tile,
                            Properties = customProperties
                        });
                    }
                }

                map.Terrains = terrainTypes.ToArray();

                List<Tile> tiles = new List<Tile>();
                foreach (var tileElem in tElem.Elements("tile"))
                {
                    UInt32 id = Convert.ToUInt32(tileElem.Attribute("id").Value);
                    Tile tile = mapTiles[gid2id[id + FirstGID]];
                    var terrain = tileElem.Attribute("terrain")?.Value;
                    if (terrain != null)
                    {
                        var terrains = terrain.Split(',')
                            .Select(f => f != "" ? terrainTypes[int.Parse(f)] : null)
                            .ToArray();
                        tile.Terrain = new TerrainData(terrains[0], terrains[1], terrains[2], terrains[3]);
                    }

                    if (tileElem.Element("properties") != null)
                        foreach (var pElem in tileElem.Element("properties").Elements("property"))
                            tile.Properties.Add(pElem.Attribute("name").Value, Property.Create(pElem.Attribute("value").Value));
                    tiles.Add(tile);
                }
                t.Tiles = tiles.ToArray();

                t.Properties = new Dictionary<String, Property>();
                if (tElem.Element("properties") != null)
                    foreach (var pElem in tElem.Element("properties").Elements("property"))
                        t.Properties.Add(pElem.Attribute("name").Value, Property.Create(pElem.Attribute("value").Value));

                tilesets.Add(t);
            }
            map.Tilesets = tilesets.ToArray();

            TileLayerList layers = new TileLayerList();
            foreach (var lElem in input.Document.Root.Elements("layer"))
            {
                TileLayer l = new TileLayer();
                l.Name = lElem.Attribute("name") == null ? null : lElem.Attribute("name").Value;
                l.Opacity = lElem.Attribute("opacity") == null ? 1.0f : Convert.ToSingle(lElem.Attribute("opacity").Value);
                l.Visible = lElem.Attribute("visible") == null ? true : lElem.Attribute("visible").Equals("1");

                l.OpacityColor = Color.White;
                l.OpacityColor.A = Convert.ToByte(255.0f * l.Opacity);

                l.Properties = new Dictionary<String, Property>();
                if (lElem.Element("properties") != null)
                    foreach (var pElem in lElem.Element("properties").Elements("property"))
                        l.Properties.Add(pElem.Attribute("name").Value, Property.Create(pElem.Attribute("value").Value));

                TileData[][] tiles = new TileData[map.Orientation == FuncWorks.XNA.XTiled.MapOrientation.Orthogonal ? map.Width : map.Height + map.Width - 1][];
                for (int i = 0; i < tiles.Length; i++)
                    tiles[i] = new TileData[map.Orientation == FuncWorks.XNA.XTiled.MapOrientation.Orthogonal ? map.Height : map.Height + map.Width - 1];

                if (lElem.Element("data") != null)
                {
                    List<UInt32> gids = new List<UInt32>();
                    if (lElem.Element("data").Attribute("encoding") != null || lElem.Element("data").Attribute("compression") != null)
                    {

                        // parse csv formatted data
                        if (lElem.Element("data").Attribute("encoding") != null && lElem.Element("data").Attribute("encoding").Value.Equals("csv"))
                        {
                            foreach (var gid in lElem.Element("data").Value.Split(",\n\r".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                                gids.Add(Convert.ToUInt32(gid));
                        }
                        else if (lElem.Element("data").Attribute("encoding") != null && lElem.Element("data").Attribute("encoding").Value.Equals("base64"))
                        {
                            Byte[] data = Convert.FromBase64String(lElem.Element("data").Value);

                            if (lElem.Element("data").Attribute("compression") == null)
                            {
                                // uncompressed data
                                for (int i = 0; i < data.Length; i += sizeof(UInt32))
                                {
                                    gids.Add(BitConverter.ToUInt32(data, i));
                                }
                            }
                            else if (lElem.Element("data").Attribute("compression").Value.Equals("gzip"))
                            {
                                // gzip data
                                GZipStream gz = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
                                Byte[] buffer = new Byte[sizeof(UInt32)];
                                while (gz.Read(buffer, 0, buffer.Length) == buffer.Length)
                                {
                                    gids.Add(BitConverter.ToUInt32(buffer, 0));
                                }
                            }
                            else if (lElem.Element("data").Attribute("compression").Value.Equals("zlib"))
                            {
                                // zlib data - first two bytes zlib specific and not part of deflate
                                MemoryStream ms = new MemoryStream(data);
                                ms.ReadByte();
                                ms.ReadByte();
                                DeflateStream gz = new DeflateStream(ms, CompressionMode.Decompress);
                                Byte[] buffer = new Byte[sizeof(UInt32)];
                                while (gz.Read(buffer, 0, buffer.Length) == buffer.Length)
                                {
                                    gids.Add(BitConverter.ToUInt32(buffer, 0));
                                }
                            }
                            else
                            {
                                throw new NotSupportedException(String.Format("Compression '{0}' not supported.  XTiled supports gzip or zlib", lElem.Element("data").Attribute("compression").Value));
                            }
                        }
                        else
                        {
                            throw new NotSupportedException(String.Format("Encoding '{0}' not supported.  XTiled supports csv or base64", lElem.Element("data").Attribute("encoding").Value));
                        }
                    }
                    else
                    {

                        // parse xml formatted data
                        foreach (var tElem in lElem.Element("data").Elements("tile"))
                            gids.Add(Convert.ToUInt32(tElem.Attribute("gid").Value));
                    }

                    for (int i = 0; i < gids.Count; i++)
                    {
                        TileData td = new TileData();
                        UInt32 ID = gids[i] & ~(FLIPPED_HORIZONTALLY_FLAG | FLIPPED_VERTICALLY_FLAG | FLIPPED_DIAGONALLY_FLAG);
                        td.SourceID = gid2id[ID];
                        if (td.SourceID >= 0)
                        {
                            Boolean FlippedHorizontally = Convert.ToBoolean(gids[i] & FLIPPED_HORIZONTALLY_FLAG);
                            Boolean FlippedVertically = Convert.ToBoolean(gids[i] & FLIPPED_VERTICALLY_FLAG);
                            Boolean FlippedDiagonally = Convert.ToBoolean(gids[i] & FLIPPED_DIAGONALLY_FLAG);

                            if (FlippedDiagonally)
                            {
                                td.Rotation = MathHelper.PiOver2;
                                // this works, not 100% why (we are rotating instead of diag flipping, so I guess that's a clue)
                                FlippedHorizontally = false;
                            }
                            else
                                td.Rotation = 0;

                            if (FlippedVertically && FlippedHorizontally)
                                td.Effects = SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically;
                            else if (FlippedVertically)
                                td.Effects = SpriteEffects.FlipVertically;
                            else if (FlippedHorizontally)
                                td.Effects = SpriteEffects.FlipHorizontally;
                            else
                                td.Effects = SpriteEffects.None;

                            td.Target.Width = mapTiles[td.SourceID].Source.Width;
                            td.Target.Height = mapTiles[td.SourceID].Source.Height;

                            if (map.Orientation == FuncWorks.XNA.XTiled.MapOrientation.Orthogonal)
                            {
                                Int32 x = i % map.Width;
                                Int32 y = i / map.Width;
                                td.Target.X = x * map.TileWidth + Convert.ToInt32(mapTiles[td.SourceID].Origin.X) + map.Tilesets[mapTiles[td.SourceID].TilesetID].TileOffsetX;
                                td.Target.Y = y * map.TileHeight + Convert.ToInt32(mapTiles[td.SourceID].Origin.Y) + map.Tilesets[mapTiles[td.SourceID].TilesetID].TileOffsetY;
                                td.Target.Y += map.TileHeight - td.Target.Height;

                                // adjust for off center origin in odd tiles sizes
                                if (FlippedDiagonally && td.Target.Width % 2 == 1)
                                    td.Target.X += 1;

                                tiles[x][y] = td;
                            }
                            else if (map.Orientation == FuncWorks.XNA.XTiled.MapOrientation.Isometric)
                            {
                                Int32 x = map.Height + i % map.Width - (1 * i / map.Width + 1);
                                Int32 y = i - i / map.Width * map.Width + i / map.Width;
                                td.Target.X = x * map.TileWidth + Convert.ToInt32(mapTiles[td.SourceID].Origin.X) + map.Tilesets[mapTiles[td.SourceID].TilesetID].TileOffsetX;
                                td.Target.Y = y * map.TileHeight + Convert.ToInt32(mapTiles[td.SourceID].Origin.Y) + map.Tilesets[mapTiles[td.SourceID].TilesetID].TileOffsetY;
                                td.Target.Y += map.TileHeight - td.Target.Height;
                                td.Target.X = td.Target.X / 2 + map.TileWidth / 4;
                                td.Target.Y = td.Target.Y / 2 + map.TileHeight / 4;

                                // adjust for off center origin in odd tiles sizes
                                if (FlippedDiagonally && td.Target.Width % 2 == 1)
                                    td.Target.X += 1;

                                tiles[x][y] = td;
                            }
                        }
                    }
                }
                l.Tiles = tiles;

                layers.Add(l);
            }
            map.TileLayers = layers;
            map.SourceTiles = mapTiles.ToArray();

            ObjectLayerList oLayers = new ObjectLayerList();
            foreach (var olElem in input.Document.Root.Elements("objectgroup"))
            {
                ObjectLayer ol = new ObjectLayer();
                ol.Name = olElem.Attribute("name") == null ? null : olElem.Attribute("name").Value;
                ol.Opacity = olElem.Attribute("opacity") == null ? 1.0f : Convert.ToSingle(olElem.Attribute("opacity").Value);
                ol.Visible = olElem.Attribute("visible") == null ? true : olElem.Attribute("visible").Equals("1");

                ol.OpacityColor = Color.White;
                ol.OpacityColor.A = Convert.ToByte(255.0f * ol.Opacity);

                ol.Color = null;
                if (olElem.Attribute("color") != null)
                {
                    System.Drawing.Color sdc = System.Drawing.ColorTranslator.FromHtml("#" + olElem.Attribute("color").Value.TrimStart('#'));
                    ol.Color = new Color(sdc.R, sdc.G, sdc.B, ol.OpacityColor.A);
                }

                ol.Properties = new Dictionary<String, Property>();
                if (olElem.Element("properties") != null)
                    foreach (var pElem in olElem.Element("properties").Elements("property"))
                        ol.Properties.Add(pElem.Attribute("name").Value, Property.Create(pElem.Attribute("value").Value));

                List<MapObject> objects = new List<MapObject>();
                foreach (var oElem in olElem.Elements("object"))
                {
                    MapObject o = new MapObject();
                    o.Name = oElem.Attribute("name") == null ? null : oElem.Attribute("name").Value;
                    o.Type = oElem.Attribute("type") == null ? null : oElem.Attribute("type").Value;
                    o.Bounds.X = oElem.Attribute("x") == null ? 0 : Convert.ToInt32(oElem.Attribute("x").Value);
                    o.Bounds.Y = oElem.Attribute("y") == null ? 0 : Convert.ToInt32(oElem.Attribute("y").Value);
                    o.Bounds.Width = oElem.Attribute("width") == null ? 0 : Convert.ToInt32(oElem.Attribute("width").Value);
                    o.Bounds.Height = oElem.Attribute("height") == null ? 0 : Convert.ToInt32(oElem.Attribute("height").Value);
                    o.TileID = oElem.Attribute("gid") == null ? null : (Int32?)gid2id[Convert.ToUInt32(oElem.Attribute("gid").Value)];
                    o.Visible = oElem.Attribute("visible") == null ? true : oElem.Attribute("visible").Equals("1");

                    if (o.TileID.HasValue)
                    {
                        o.Bounds.X += Convert.ToInt32(mapTiles[o.TileID.Value].Origin.X); // +map.Tilesets[mapTiles[o.TileID.Value].TilesetID].TileOffsetX;
                        o.Bounds.Y -= Convert.ToInt32(mapTiles[o.TileID.Value].Origin.Y); // +map.Tilesets[mapTiles[o.TileID.Value].TilesetID].TileOffsetY;
                        o.Bounds.Width = map.SourceTiles[o.TileID.Value].Source.Width;
                        o.Bounds.Height = map.SourceTiles[o.TileID.Value].Source.Height;
                    }

                    o.Properties = new Dictionary<String, Property>();
                    if (oElem.Element("properties") != null)
                        foreach (var pElem in oElem.Element("properties").Elements("property"))
                            o.Properties.Add(pElem.Attribute("name").Value, Property.Create(pElem.Attribute("value").Value));

                    o.Polygon = null;
                    if (oElem.Element("polygon") != null)
                    {
                        List<Point> points = new List<Point>();
                        foreach (var point in oElem.Element("polygon").Attribute("points").Value.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                        {
                            String[] coord = point.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                            points.Add(new Point(o.Bounds.X + Convert.ToInt32(coord[0]), o.Bounds.Y + Convert.ToInt32(coord[1])));
                        }
                        points.Add(points.First()); // connect the last point to the first and close the polygon
                        o.Polygon = Polygon.FromPoints(points);
                        o.Bounds = o.Polygon.Bounds;
                    }

                    o.Polyline = null;
                    if (oElem.Element("polyline") != null)
                    {
                        List<Point> points = new List<Point>();
                        foreach (var point in oElem.Element("polyline").Attribute("points").Value.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                        {
                            String[] coord = point.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                            points.Add(new Point(o.Bounds.X + Convert.ToInt32(coord[0]), o.Bounds.Y + Convert.ToInt32(coord[1])));
                        }

                        o.Polyline = Polyline.FromPoints(points);
                        o.Bounds = o.Polyline.Bounds;
                    }

                    objects.Add(o);
                }
                ol.MapObjects = objects.ToArray();

                oLayers.Add(ol);
            }
            map.ObjectLayers = oLayers;

            Int32 layerId = 0, objectId = 0;
            List<LayerInfo> info = new List<LayerInfo>();
            foreach (var elem in input.Document.Root.Elements())
            {
                if (elem.Name.LocalName.Equals("layer"))
                    info.Add(new LayerInfo() { ID = layerId++, LayerType = LayerType.TileLayer });
                else if (elem.Name.LocalName.Equals("objectgroup"))
                    info.Add(new LayerInfo() { ID = objectId++, LayerType = LayerType.ObjectLayer });
            }
            map.LayerOrder = info.ToArray();

            Thread.CurrentThread.CurrentCulture = culture;
            return map;
        }

        private static Color HexColorToRGBColor(string hexColorString)
        {
            //replace # occurences
            if (hexColorString.IndexOf('#') != -1)
                hexColorString = hexColorString.Replace("#", "");

            int r, g, b = 0;

            r = int.Parse(hexColorString.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            g = int.Parse(hexColorString.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            b = int.Parse(hexColorString.Substring(4, 2), NumberStyles.AllowHexSpecifier);

            return new Color(r, g, b);
        }
    }
}
