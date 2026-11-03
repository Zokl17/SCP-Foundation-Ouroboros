using System;

namespace ScpFoundation.Storytelling
{
    // RimWorld north is increasing Z. Keep the source's north-east corner.
    public sealed class Scp882MapWindow
    {
        public const int SourceSize = 250;
        public readonly int Width, Height, SourceX, SourceZ, TargetX, TargetZ;

        public Scp882MapWindow(int mapWidth, int mapHeight)
        {
            if (mapWidth <= 0 || mapHeight <= 0) throw new ArgumentOutOfRangeException();
            Width = Math.Min(SourceSize, mapWidth);
            Height = Math.Min(SourceSize, mapHeight);
            SourceX = SourceSize - Width;
            SourceZ = SourceSize - Height;
            TargetX = mapWidth - Width;
            TargetZ = mapHeight - Height;
        }

        public int MapX(int sourceX) { return sourceX - SourceX + TargetX; }
        public int MapZ(int sourceZ) { return sourceZ - SourceZ + TargetZ; }

        public int SourceByteIndex(int mapX, int mapZ)
        {
            int x = mapX - TargetX, z = mapZ - TargetZ;
            if (x < 0 || x >= Width || z < 0 || z >= Height)
                throw new ArgumentOutOfRangeException();
            return ((z + SourceZ) * SourceSize + x + SourceX) * 2;
        }
    }
}
