using System.Collections.Generic;
using UnityEngine;

namespace Shiftling
{
    /// <summary>Tiny pixel canvas used to paint every sprite in the game procedurally.</summary>
    public class Px
    {
        public readonly int w, h;
        public readonly Color[] c;

        public Px(int w, int h)
        {
            this.w = w; this.h = h;
            c = new Color[w * h];
            for (int i = 0; i < c.Length; i++) c[i] = Color.clear;
        }

        public void Set(int x, int y, Color col)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            c[y * w + x] = col;
        }

        public Color Get(int x, int y) => (x < 0 || y < 0 || x >= w || y >= h) ? Color.clear : c[y * w + x];

        public void Rect(int x0, int y0, int x1, int y1, Color col)
        {
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, col);
        }

        public void Disc(float cx, float cy, float r, Color col)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r) Set(x, y, col);
                }
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color col)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, col);
                }
        }

        /// <summary>Darkens every opaque pixel that borders a transparent one (outline).</summary>
        public void Outline(Color col)
        {
            var copy = (Color[])c.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (copy[y * w + x].a > 0.01f) continue;
                    bool n = false;
                    for (int k = 0; k < 4 && !n; k++)
                    {
                        int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx >= 0 && ny >= 0 && nx < w && ny < h && copy[ny * w + nx].a > 0.5f) n = true;
                    }
                    if (n) c[y * w + x] = col;
                }
        }

        public Sprite ToSprite(float ppu = 16, Vector2? pivot = null)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            t.SetPixels(c);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }
    }

    public class Palette
    {
        public Color dirt, dirtDark, dirtLight, top, topDark, skyTop, skyBottom, hillFar, hillNear, accent;
    }

    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static float Hash(int x, int y, int s = 0)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffff) / 65535f;
            }
        }

        static Sprite Cached(string key, System.Func<Sprite> make)
        {
            if (!cache.TryGetValue(key, out var s)) { s = make(); cache[key] = s; }
            return s;
        }

        static Color C(float r, float g, float b, float a = 1) => new Color(r, g, b, a);
        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public static readonly Palette[] Palettes =
        {
            new Palette { dirt = Hex("#5b3d2e"), dirtDark = Hex("#432c22"), dirtLight = Hex("#7a5440"), top = Hex("#59c135"), topDark = Hex("#3a8a2b"),
                          skyTop = Hex("#1b2a4a"), skyBottom = Hex("#5b7fb0"), hillFar = Hex("#2f4a6e"), hillNear = Hex("#22374f"), accent = Hex("#9ef0ff") },
            new Palette { dirt = Hex("#4d4f5c"), dirtDark = Hex("#373844"), dirtLight = Hex("#686b7a"), top = Hex("#8fa6a0"), topDark = Hex("#61756f"),
                          skyTop = Hex("#16161f"), skyBottom = Hex("#4b4a63"), hillFar = Hex("#35344a"), hillNear = Hex("#25243a"), accent = Hex("#d8cfc0") },
            new Palette { dirt = Hex("#2e2445"), dirtDark = Hex("#211a33"), dirtLight = Hex("#43365f"), top = Hex("#6b5fb5"), topDark = Hex("#4a3f8a"),
                          skyTop = Hex("#07061a"), skyBottom = Hex("#2b2457"), hillFar = Hex("#1d1a40"), hillNear = Hex("#131030"), accent = Hex("#c9a2ff") },
            new Palette { dirt = Hex("#4a2620"), dirtDark = Hex("#331915"), dirtLight = Hex("#6b3a2c"), top = Hex("#a8452a"), topDark = Hex("#76301f"),
                          skyTop = Hex("#1a0a0a"), skyBottom = Hex("#7a2e1d"), hillFar = Hex("#4a1c16"), hillNear = Hex("#2e110e"), accent = Hex("#ffb35a") },
            new Palette { dirt = Hex("#3b3150"), dirtDark = Hex("#2a223a"), dirtLight = Hex("#54476e"), top = Hex("#e08bb5"), topDark = Hex("#a95f88"),
                          skyTop = Hex("#120f2a"), skyBottom = Hex("#c46a6a"), hillFar = Hex("#4f3560"), hillNear = Hex("#2e2040"), accent = Hex("#ffd27a") },
        };

        // ------------------------------------------------------------------ tiles
        public static Sprite Ground(int pal, bool topExposed, int variant)
        {
            return Cached($"g{pal}{topExposed}{variant}", () =>
            {
                var p = Palettes[pal];
                var px = new Px(16, 16);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float n = Hash(x, y, variant * 7 + pal);
                        Color col = n < 0.15f ? p.dirtDark : n > 0.9f ? p.dirtLight : p.dirt;
                        px.Set(x, y, col);
                    }
                // a few pebbles
                for (int i = 0; i < 3; i++)
                {
                    int sx = (int)(Hash(i, variant, 99) * 13) + 1, sy = (int)(Hash(variant, i, 77) * 9) + 1;
                    px.Rect(sx, sy, sx + 1, sy, p.dirtLight);
                    px.Set(sx, sy - 1, p.dirtDark);
                }
                if (topExposed)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        int depth = 3 + (int)(Hash(x, variant, 5) * 3);
                        for (int d = 0; d < depth; d++) px.Set(x, 15 - d, d == depth - 1 ? p.topDark : p.top);
                        if (Hash(x, variant, 9) > 0.7f) px.Set(x, 15, Color.Lerp(p.top, Color.white, 0.35f));
                    }
                }
                return px.ToSprite();
            });
        }

        public static Sprite Spikes(bool up)
        {
            return Cached("spk" + up, () =>
            {
                var px = new Px(16, 16);
                Color lt = C(0.85f, 0.88f, 0.95f), dk = C(0.45f, 0.48f, 0.58f);
                for (int s = 0; s < 2; s++)
                    for (int x = 0; x < 8; x++)
                    {
                        float hgt = 10f - Mathf.Abs(x - 3.5f) * 2.6f;
                        for (int y = 0; y < hgt; y++)
                        {
                            int yy = up ? y : 15 - y;
                            px.Set(s * 8 + x, yy, x < 4 ? lt : dk);
                        }
                    }
                px.Outline(C(0.12f, 0.12f, 0.18f));
                return px.ToSprite();
            });
        }

        public static Sprite Liquid(bool lava, bool surface, int frame)
        {
            return Cached($"liq{lava}{surface}{frame}", () =>
            {
                var px = new Px(16, 16);
                Color body = lava ? C(0.95f, 0.32f, 0.08f, 0.8f) : C(0.18f, 0.48f, 0.9f, 0.62f);
                Color hi = lava ? C(1f, 0.75f, 0.2f) : C(0.7f, 0.9f, 1f, 0.85f);
                Color mid = lava ? C(1f, 0.5f, 0.12f, 0.85f) : C(0.3f, 0.6f, 0.95f, 0.65f);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float n = Hash(x + frame * 3, y, lava ? 3 : 4);
                        px.Set(x, y, n > 0.92f ? mid : body);
                    }
                if (surface)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        int top = 13 + Mathf.RoundToInt(Mathf.Sin((x + frame * 4) * 0.8f) * 1.2f);
                        for (int y = top + 1; y < 16; y++) px.Set(x, y, Color.clear);
                        px.Set(x, top, hi);
                        px.Set(x, top - 1, mid);
                    }
                }
                return px.ToSprite();
            });
        }

        public static Sprite Cracked(int pal)
        {
            return Cached("crk" + pal, () =>
            {
                var p = Palettes[pal];
                var px = new Px(16, 16);
                Color st = Color.Lerp(p.dirtLight, C(0.6f, 0.6f, 0.62f), 0.6f);
                Color dk = Color.Lerp(st, Color.black, 0.35f);
                px.Rect(0, 0, 15, 15, st);
                px.Rect(0, 0, 15, 0, dk); px.Rect(0, 15, 15, 15, Color.Lerp(st, Color.white, 0.25f));
                int x = 4, y = 15;
                for (int i = 0; i < 18; i++)
                {
                    px.Set(x, y, C(0.1f, 0.1f, 0.12f));
                    y--; x += Hash(i, 3, 11) > 0.5f ? 1 : -1; x = Mathf.Clamp(x, 1, 14);
                }
                x = 11; y = 12;
                for (int i = 0; i < 8; i++) { px.Set(x, y, C(0.1f, 0.1f, 0.12f)); x++; y -= Hash(i, 7, 2) > 0.4f ? 1 : 0; }
                return px.ToSprite();
            });
        }

        public static Sprite Barrier()
        {
            return Cached("bar", () =>
            {
                var px = new Px(16, 16);
                Color w1 = Hex("#a0673a"), w2 = Hex("#7d4d2a"), w3 = Hex("#c58a54");
                for (int x = 0; x < 16; x++)
                    for (int y = 0; y < 16; y++)
                    {
                        int plank = x / 5;
                        Color col = (x % 5 == 0) ? w2 : (Hash(x, y, plank) > 0.85f ? w3 : w1);
                        px.Set(x, y, col);
                    }
                px.Rect(0, 3, 15, 4, w2); px.Rect(0, 11, 15, 12, w2);
                px.Set(2, 4, C(0.8f, 0.8f, 0.8f)); px.Set(12, 11, C(0.8f, 0.8f, 0.8f));
                return px.ToSprite();
            });
        }

        public static Sprite Crate()
        {
            return Cached("crate", () =>
            {
                var px = new Px(16, 16);
                Color f = Hex("#6e4423"), b = Hex("#b07a43"), l = Hex("#d6a064");
                px.Rect(0, 0, 15, 15, f);
                px.Rect(2, 2, 13, 13, b);
                for (int i = 2; i < 14; i++) { px.Set(i, i, f); px.Set(i, 15 - i, f); px.Set(i + 1, i, l); }
                px.Rect(0, 15, 15, 15, l);
                return px.ToSprite();
            });
        }

        public static Sprite Plate(bool pressed)
        {
            return Cached("plate" + pressed, () =>
            {
                var px = new Px(16, 16);
                px.Rect(0, 0, 15, 1, C(0.25f, 0.25f, 0.3f));
                int hgt = pressed ? 2 : 4;
                Color col = pressed ? C(0.4f, 1f, 0.6f) : C(1f, 0.45f, 0.4f);
                px.Rect(2, 2, 13, hgt, col);
                px.Rect(2, hgt, 13, hgt, Color.Lerp(col, Color.white, 0.4f));
                return px.ToSprite();
            });
        }

        public static Sprite Door()
        {
            return Cached("door", () =>
            {
                var px = new Px(16, 16);
                Color fr = Hex("#3c2f5c"), bar = Hex("#8f7bd6"), hl = Hex("#c9bcff");
                px.Rect(0, 0, 15, 15, fr);
                for (int x = 2; x < 16; x += 4) { px.Rect(x, 0, x + 1, 15, bar); px.Rect(x, 0, x, 15, hl); }
                px.Rect(0, 7, 15, 8, bar);
                return px.ToSprite();
            });
        }

        public static Sprite Fan(int frame)
        {
            return Cached("fan" + frame, () =>
            {
                var px = new Px(16, 16);
                Color fr = Hex("#4a5160"), dk = Hex("#2a2f3a"), bl = Hex("#b9c4d6");
                px.Rect(0, 0, 15, 15, fr);
                px.Rect(1, 1, 14, 14, dk);
                for (int i = 0; i < 4; i++)
                {
                    float a = (i * 90 + frame * 45) * Mathf.Deg2Rad;
                    for (float r = 1; r < 6; r += 0.5f)
                        px.Set(8 + Mathf.RoundToInt(Mathf.Cos(a) * r), 8 + Mathf.RoundToInt(Mathf.Sin(a) * r * 0.5f), bl);
                }
                px.Rect(7, 7, 8, 8, Color.white);
                px.Rect(0, 15, 15, 15, bl);
                return px.ToSprite();
            });
        }

        public static Sprite Pedestal()
        {
            return Cached("ped", () =>
            {
                var px = new Px(16, 16);
                Color s = Hex("#8a8fa3"), d = Hex("#5d6175"), l = Hex("#c3c8da");
                px.Rect(2, 0, 13, 2, d); px.Rect(4, 3, 11, 6, s); px.Rect(3, 7, 12, 8, l);
                px.Rect(4, 3, 4, 6, l);
                return px.ToSprite();
            });
        }

        public static Sprite Checkpoint(bool on)
        {
            return Cached("cp" + on, () =>
            {
                var px = new Px(16, 16);
                px.Rect(7, 0, 8, 9, C(0.4f, 0.4f, 0.48f));
                px.Rect(5, 0, 10, 1, C(0.3f, 0.3f, 0.36f));
                Color cr = on ? C(0.5f, 1f, 0.85f) : C(0.45f, 0.45f, 0.55f);
                for (int y = 0; y < 6; y++)
                {
                    int half = 3 - Mathf.Abs(y - 2);
                    if (y > 2) half = 5 - y;
                    px.Rect(8 - half - 1, 9 + y, 7 + half + 1, 9 + y, cr);
                }
                px.Set(7, 12, Color.white);
                return px.ToSprite();
            });
        }

        public static Sprite Portal()
        {
            return Cached("portal", () =>
            {
                var px = new Px(32, 32);
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float dx = x + 0.5f - 16, dy = y + 0.5f - 16;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float ang = Mathf.Atan2(dy, dx);
                        if (d < 14)
                        {
                            float sw = Mathf.Sin(ang * 3 + d * 0.6f);
                            Color col = Color.Lerp(C(0.35f, 0.1f, 0.7f), C(1f, 0.8f, 1f), Mathf.Clamp01(sw * 0.5f + 0.5f) * (1 - d / 14f) + (d > 12 ? 0.6f : 0));
                            col.a = d > 12.5f ? 1 : 0.85f;
                            px.Set(x, y, col);
                        }
                    }
                return px.ToSprite();
            });
        }

        public static Sprite Glow()
        {
            return Cached("glow", () =>
            {
                var px = new Px(32, 32);
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float dx = x + 0.5f - 16, dy = y + 0.5f - 16;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / 16f;
                        float a = Mathf.Clamp01(1 - d);
                        px.Set(x, y, new Color(1, 1, 1, a * a * 0.8f));
                    }
                var s = px.ToSprite();
                s.texture.filterMode = FilterMode.Bilinear;
                return s;
            });
        }

        public static Sprite Dot()
        {
            return Cached("dot", () =>
            {
                var px = new Px(2, 2);
                px.Rect(0, 0, 1, 1, Color.white);
                return px.ToSprite(16);
            });
        }

        public static Sprite White()
        {
            return Cached("white", () =>
            {
                var px = new Px(4, 4);
                px.Rect(0, 0, 3, 3, Color.white);
                return px.ToSprite(4);
            });
        }

        public static Sprite Tuft(int pal, int variant)
        {
            return Cached($"tuft{pal}{variant}", () =>
            {
                var p = Palettes[pal];
                var px = new Px(16, 16);
                int kind = variant % 3;
                if (kind == 0)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        int x = 3 + i * 2 + (int)(Hash(i, variant, 1) * 2);
                        int hgt = 2 + (int)(Hash(i, variant, 2) * 4);
                        px.Rect(x, 0, x, hgt, i % 2 == 0 ? p.top : p.topDark);
                    }
                }
                else if (kind == 1)
                {
                    px.Rect(7, 0, 7, 4, p.topDark);
                    px.Disc(7.5f, 5.5f, 1.6f, p.accent);
                    px.Set(7, 5, Color.white);
                }
                else
                {
                    px.Ellipse(8, 1.5f, 4, 2.2f, Color.Lerp(p.dirtLight, Color.gray, 0.5f));
                    px.Rect(6, 2, 7, 2, Color.Lerp(p.dirtLight, Color.white, 0.3f));
                }
                return px.ToSprite(16, new Vector2(0.5f, 0f));
            });
        }

        // ---------------------------------------------------------------- background
        public static Sprite Sky(int pal)
        {
            return Cached("sky" + pal, () =>
            {
                var p = Palettes[pal];
                var px = new Px(4, 128);
                for (int y = 0; y < 128; y++)
                {
                    var col = Color.Lerp(p.skyBottom, p.skyTop, Mathf.SmoothStep(0, 1, y / 127f));
                    px.Rect(0, y, 3, y, col);
                }
                var s = px.ToSprite(4);
                s.texture.filterMode = FilterMode.Bilinear;
                return s;
            });
        }

        public static Sprite Stars(int seed)
        {
            return Cached("stars" + seed, () =>
            {
                var px = new Px(256, 128);
                for (int i = 0; i < 90; i++)
                {
                    int x = (int)(Hash(i, seed, 1) * 256), y = (int)(Hash(seed, i, 2) * 128);
                    float b = 0.4f + Hash(i, i, 3) * 0.6f;
                    px.Set(x, y, new Color(1, 1, 1, b));
                    if (b > 0.9f) { px.Set(x + 1, y, new Color(1, 1, 1, 0.4f)); px.Set(x - 1, y, new Color(1, 1, 1, 0.4f)); px.Set(x, y + 1, new Color(1, 1, 1, 0.4f)); px.Set(x, y - 1, new Color(1, 1, 1, 0.4f)); }
                }
                return px.ToSprite(16);
            });
        }

        public static Sprite Hills(int pal, bool near)
        {
            return Cached($"hills{pal}{near}", () =>
            {
                var p = Palettes[pal];
                var px = new Px(256, 96);
                Color col = near ? p.hillNear : p.hillFar;
                float s = near ? 1.7f : 1f;
                for (int x = 0; x < 256; x++)
                {
                    float t = x / 256f * Mathf.PI * 2;
                    float hgt = 40 + Mathf.Sin(t * 2 * s) * 14 + Mathf.Sin(t * 5 + 1.3f) * 7 + Mathf.Sin(t * 11 + pal) * 3;
                    if (near) hgt -= 12;
                    for (int y = 0; y < hgt && y < 96; y++) px.Set(x, y, col);
                    // little trees on the near layer
                    if (near && Hash(x, pal, 8) > 0.93f)
                    {
                        int top = (int)hgt;
                        for (int k = 0; k < 7; k++) px.Rect(x - (7 - k) / 3, top + k, x + (7 - k) / 3, top + k, col);
                    }
                }
                return px.ToSprite(16, new Vector2(0.5f, 0f));
            });
        }

        // ---------------------------------------------------------------- creatures
        static void Eyes(Px px, int lx, int rx, int y, Color pupil)
        {
            px.Rect(lx, y, lx + 1, y + 1, Color.white); px.Set(lx + 1, y, pupil);
            px.Rect(rx, y, rx + 1, y + 1, Color.white); px.Set(rx + 1, y, pupil);
        }

        public static Sprite FormSprite(Form f, int frame = 0)
        {
            return Cached($"form{f}{frame}", () =>
            {
                Color outline = C(0.08f, 0.07f, 0.12f);
                switch (f)
                {
                    case Form.Wisp:
                    {
                        var px = new Px(16, 16);
                        Color b = Hex("#5fe6ff"), l = Hex("#c8fbff"), d = Hex("#2aa4d6");
                        px.Disc(8, 6.5f, 5.6f, b);
                        // flame-like tufts on top
                        int sway = frame == 0 ? 0 : 1;
                        px.Rect(5 + sway, 11, 6 + sway, 13, b); px.Rect(8, 11, 9, 14 + sway, b); px.Rect(11 - sway, 11, 11 - sway, 12, b);
                        px.Disc(6.5f, 8, 2.2f, l);
                        px.Rect(3, 2, 12, 2, d);
                        Eyes(px, 5, 9, 6, outline);
                        px.Outline(outline);
                        return px.ToSprite();
                    }
                    case Form.Frog:
                    {
                        var px = new Px(16, 16);
                        Color b = Hex("#62c94a"), l = Hex("#b8f07a"), d = Hex("#3c8a2f");
                        px.Ellipse(8, 4.5f, 7, 4.2f, b);
                        px.Ellipse(8, 3.5f, 4.5f, 2.2f, l);
                        px.Disc(4.5f, 8.5f, 2.6f, b); px.Disc(11.5f, 8.5f, 2.6f, b);
                        px.Rect(3, 8, 5, 9, Color.white); px.Rect(10, 8, 12, 9, Color.white);
                        px.Set(4, 8, outline); px.Set(11, 8, outline);
                        px.Rect(5, 5, 10, 5, d);
                        if (frame == 1) { px.Rect(0, 0, 2, 1, d); px.Rect(13, 0, 15, 1, d); }
                        else { px.Rect(1, 0, 3, 0, d); px.Rect(12, 0, 14, 0, d); }
                        px.Outline(outline);
                        return px.ToSprite(16, new Vector2(0.5f, 0.5f));
                    }
                    case Form.Boulder:
                    {
                        var px = new Px(32, 32);
                        Color b = Hex("#8f8579"), l = Hex("#bdb2a3"), d = Hex("#5e564d");
                        px.Disc(16, 16, 14.5f, b);
                        for (int i = 0; i < 40; i++)
                        {
                            int x = (int)(Hash(i, 1, 4) * 32), y = (int)(Hash(1, i, 4) * 32);
                            if (px.Get(x, y).a > 0) px.Set(x, y, Hash(i, i, 5) > 0.5f ? d : l);
                        }
                        px.Disc(11, 22, 4, l);
                        px.Rect(18, 8, 24, 8, d); px.Rect(24, 9, 25, 11, d);
                        // sleepy eyes
                        px.Rect(9, 16, 12, 17, Color.white); px.Rect(19, 16, 22, 17, Color.white);
                        px.Rect(10, 16, 11, 16, outline); px.Rect(20, 16, 21, 16, outline);
                        px.Rect(8, 18, 13, 18, d); px.Rect(18, 18, 23, 18, d);
                        px.Outline(outline);
                        return px.ToSprite(16);
                    }
                    case Form.Bat:
                    {
                        var px = new Px(16, 16);
                        Color b = Hex("#7a55c9"), l = Hex("#a98bf0"), d = Hex("#4a2f86");
                        px.Disc(8, 7, 3.4f, b);
                        px.Set(6, 11, b); px.Set(10, 11, b); px.Set(6, 10, b); px.Set(10, 10, b);
                        if (frame == 0)
                        {
                            for (int i = 0; i < 5; i++) { px.Rect(4 - i, 7 + i / 2, 4 - i, 9 + i / 2, d); px.Rect(11 + i, 7 + i / 2, 11 + i, 9 + i / 2, d); }
                        }
                        else
                        {
                            for (int i = 0; i < 5; i++) { px.Rect(4 - i, 4 - i / 2, 4 - i, 7 - i / 2, d); px.Rect(11 + i, 4 - i / 2, 11 + i, 7 - i / 2, d); }
                        }
                        px.Disc(8, 6, 1.6f, l);
                        px.Set(6, 8, Hex("#ffe066")); px.Set(9, 8, Hex("#ffe066"));
                        px.Set(7, 5, Color.white); px.Set(8, 5, Color.white);
                        px.Outline(outline);
                        return px.ToSprite();
                    }
                    default: // Ember
                    {
                        var px = new Px(16, 16);
                        Color o = Hex("#ff7a1f"), y = Hex("#ffd34d"), r = Hex("#d83a1a");
                        px.Disc(8, 5.5f, 5f, o);
                        int tip = frame == 0 ? 0 : 1;
                        for (int k = 0; k < 7; k++)
                        {
                            int half = Mathf.Max(0, 4 - k * 4 / 7);
                            px.Rect(8 - half + (k > 3 ? tip : 0), 8 + k, 7 + half + (k > 3 ? tip : 0), 8 + k, o);
                        }
                        px.Disc(8, 4.5f, 3f, y);
                        px.Rect(4, 1, 11, 1, r);
                        Eyes(px, 5, 9, 5, outline);
                        px.Outline(Hex("#5a1a0a"));
                        return px.ToSprite();
                    }
                }
            });
        }
    }
}
