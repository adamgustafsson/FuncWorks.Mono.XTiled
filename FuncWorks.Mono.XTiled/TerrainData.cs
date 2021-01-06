namespace FuncWorks.XNA.XTiled
{
    /// <summary>
    /// Terrain data for a tile.
    /// </summary>
    public class TerrainData
    {
        /// <summary>
        /// Terrain associated with the tile's top-left corner.
        /// </summary>
        public readonly Terrain TopLeft;
        /// <summary>
        /// Terrain associated with the tile's top-right corner.
        /// </summary>
        public readonly Terrain TopRight;
        /// <summary>
        /// Terrain associated with the tile's bottom-left corner.
        /// </summary>
        public readonly Terrain BottomLeft;
        /// <summary>
        /// Terrain associated with the tile's bottom-right corner.
        /// </summary>
        public readonly Terrain BottomRight;

        public TerrainData(Terrain topLeft, Terrain topRight, Terrain bottomLeft, Terrain bottomRight)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomLeft = bottomLeft;
            BottomRight = bottomRight;
        }
    }
}
