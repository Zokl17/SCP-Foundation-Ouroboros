using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    // Small UI glyphs drawn in code; approved art-kit PNGs remain untouched.
    [StaticConstructorOnStartup]
    public static class ScpIcons
    {
        private static readonly Texture2D[] cache = new Texture2D[6];
        public static Texture2D Get(int kind)
        {
            if (cache[kind] != null) return cache[kind];
            Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            texture.name = "ScpCommand" + kind;
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                bool ink = false, accent = false;
                float dx = x - 32, dy = y - 32;
                if (kind == 0) // Eye: stationary watch.
                {
                    float ellipse = dx * dx / 625f + dy * dy / 196f;
                    ink = ellipse > 0.72f && ellipse < 1f;
                    accent = dx * dx + dy * dy < 64;
                }
                if (kind == 1) // Magnifier: research.
                {
                    float circle = (x - 26) * (x - 26) + (y - 38) * (y - 38);
                    ink = circle > 150 && circle < 250 || x >= 36 && x <= 54 && Mathf.Abs(y - (64 - x)) < 4;
                    accent = x >= 19 && x <= 33 && y >= 36 && y <= 40 || x >= 24 && x <= 28 && y >= 31 && y <= 45;
                }
                if (kind == 2) // Checklist: requirements.
                {
                    ink = x >= 12 && x <= 52 && y >= 7 && y <= 57 && (x < 16 || x > 48 || y < 11 || y > 53);
                    for (int row = 0; row < 3; row++)
                    {
                        ink |= x >= 28 && x <= 43 && y >= 19 + row * 12 && y <= 21 + row * 12;
                        accent |= x >= 20 && x <= 24 && y >= 18 + row * 12 && y <= 22 + row * 12;
                    }
                }
                if (kind == 3) // Terminal screen.
                {
                    ink = x >= 7 && x <= 57 && y >= 19 && y <= 56 && (x < 11 || x > 53 || y < 23 || y > 52)
                        || x >= 29 && x <= 35 && y >= 10 && y <= 19 || x >= 19 && x <= 45 && y >= 7 && y <= 11;
                    accent = x >= 17 && x <= 33 && y >= 38 && y <= 41 || x >= 17 && x <= 43 && y >= 30 && y <= 33;
                }
                if (kind == 4) // Compact development settings.
                {
                    float radius = dx * dx + dy * dy;
                    ink = radius > 110 && radius < 260 || Mathf.Abs(dx) < 5 && Mathf.Abs(dy) < 25
                        || Mathf.Abs(dy) < 5 && Mathf.Abs(dx) < 25;
                    if (radius < 65) ink = false;
                    accent = radius < 20;
                }
                if (kind == 5) // Containment crate and direction arrow.
                {
                    ink = x >= 8 && x <= 38 && y >= 10 && y <= 38
                        && (x <= 12 || x >= 34 || y <= 14 || y >= 34)
                        || x >= 17 && x <= 29 && y >= 19 && y <= 24
                        || x >= 21 && x <= 25 && y >= 15 && y <= 30;
                    accent = x >= 40 && x <= 56 && y >= 42 && y <= 46
                        || x >= 51 && x <= 56 && y >= 37 && y <= 51
                        || x >= 45 && x <= 50 && Mathf.Abs(y - 44) <= x - 44;
                }
                texture.SetPixel(x, y, accent ? new Color(0.27f, 0.85f, 0.8f) : ink ? Color.white : Color.clear);
            }
            texture.Apply();
            cache[kind] = texture;
            return texture;
        }
    }
}
