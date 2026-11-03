using System;
using System.Collections.Generic;

namespace ScpFoundation.Runtime
{
    // N,E,S,W,NE,SE,SW,NW. Diagonals matter only between two connected sides.
    public static class WallAtlasLayout
    {
        public const int Columns = 8, Pitch = 160, Gutter = 16, Active = 128;
        public static readonly int[] Masks = MakeMasks();
        private static readonly int[] Indices = MakeIndices();

        public static int Canonical(int mask)
        {
            mask &= 255;
            if ((mask & 3) != 3) mask &= ~16;
            if ((mask & 6) != 6) mask &= ~32;
            if ((mask & 12) != 12) mask &= ~64;
            if ((mask & 9) != 9) mask &= ~128;
            return mask;
        }
        public static int Index(int mask) { return Indices[mask & 255]; }
        private static int[] MakeMasks()
        {
            var masks = new List<int>();
            for (int i = 0; i < 256; i++) if (Canonical(i) == i) masks.Add(i);
            return masks.ToArray();
        }
        private static int[] MakeIndices()
        {
            var indices = new int[256];
            for (int i = 0; i < 256; i++) indices[i] = Array.IndexOf(Masks, Canonical(i));
            return indices;
        }
    }
}
