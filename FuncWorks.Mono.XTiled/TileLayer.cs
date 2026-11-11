using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace FuncWorks.XNA.XTiled {
    /// <summary>
    /// Represents an tile layer
    /// </summary>
    public class TileLayer {
        /// <summary>
        /// Optional name of the layer
        /// </summary>
        public String Name;
        /// <summary>
        /// Opacity of the layer, 1 is opaque and 0 is complete transparent
        /// </summary>
        public Single Opacity;
        /// <summary>
        /// A color value of white, with the alpha component set to the layer Opacity
        /// </summary>
        public Color OpacityColor;
        /// <summary>
        /// True if the layer is visible
        /// </summary>
        public Boolean Visible;
        /// <summary>
        /// Custom properties
        /// </summary>
        public Dictionary<String, Property> Properties;
        /// <summary>
        /// Collection of tiles on this layer
        /// </summary>
        public TileData[][] Tiles;

        /// <summary>
        /// Returns the tile at the given cell, or null if the cell is empty or outside the layer
        /// </summary>
        /// <param name="column">Column, in tiles</param>
        /// <param name="row">Row, in tiles</param>
        /// <returns>The TileData at the cell, or null</returns>
        public TileData GetTile(Int32 column, Int32 row) {
            Boolean isInLayer = column >= 0 && column < Tiles.Length && row >= 0 && row < Tiles[column].Length;
            return isInLayer ? Tiles[column][row] : null;
        }
    }
}
