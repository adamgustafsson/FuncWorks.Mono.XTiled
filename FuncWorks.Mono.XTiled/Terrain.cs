using System;
using System.Collections.Generic;

namespace FuncWorks.XNA.XTiled
{
    /// <summary>
    /// Terrain specification.
    /// </summary>
    public class Terrain
    {
        /// <summary>
        /// Optional name of the terrain
        /// </summary>
        public string Name;
        /// <summary>
        /// Optional image tile index in the Map.SourceTiles collection
        /// </summary>
        public Int32? TileID;
        /// <summary>
        /// Custom properties
        /// </summary>
        public Dictionary<string, Property> Properties;
    }
}

