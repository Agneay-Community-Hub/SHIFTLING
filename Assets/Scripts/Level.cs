using System.Collections.Generic;
using UnityEngine;

namespace Shiftling
{
    public class Level : MonoBehaviour
    {
        public const int WindLength = 7;

        public int W, H, pal;
        public char[,] g;              // g[x,row]  (row 0 = top)
        public bool[,] wind;
        public Vector2 spawn;
        public bool doorsOpen;

        readonly Dictionary<Vector2Int, GameObject> dyn = new Dictionary<Vector2Int, GameObject>();
        readonly Dictionary<Vector2Int, SpriteRenderer> checkpoints = new Dictionary<Vector2Int, SpriteRenderer>();
        readonly List<Vector2Int> plates = new List<Vector2Int>();
        readonly List<Vector2Int> doors = new List<Vector2Int>();
        readonly List<Vector2Int> fans = new List<Vector2Int>();
        readonly List<Vector2Int> lavaTop = new List<Vector2Int>();
        readonly List<(SpriteRenderer sr, bool lava, bool surf)> liquids = new List<(SpriteRenderer, bool, bool)>();
        readonly List<SpriteRenderer> fanSr = new List<SpriteRenderer>();
        readonly List<(Transform icon, SpriteRenderer glow, float baseY)> shrines = new List<(Transform, SpriteRenderer, float)>();
        public readonly List<Crate> crates = new List<Crate>();
        readonly List<(Vector2Int cell, float t, char kind)> pending = new List<(Vector2Int, float, char)>();
        readonly HashSet<Vector2Int> pendingSet = new HashSet<Vector2Int>();
        Transform portal;
        SpriteRenderer portalGlow;
        float animT;
        int animFrame;
        PhysicsMaterial2D crateMat;

        // ----------------------------------------------------------------- coordinates
        public Vector2 World(int x, int row) => new Vector2(x, H - 1 - row);
        public Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x), H - 1 - Mathf.RoundToInt(p.y));

        public char Get(int x, int row)
        {
            if (row >= H) return '.';
            if (x < 0 || x >= W || row < 0) return '#';
            return g[x, row];
        }
        public char Get(Vector2Int c) => Get(c.x, c.y);
        public char At(Vector2 p) { var c = Cell(p); return Get(c.x, c.y); }

        public bool IsSolid(char c) => c == '#' || c == 'C' || c == 'B' || c == 'F' || (c == 'D' && !doorsOpen);
        public bool IsLiquid(char c) => c == 'W' || c == 'L';
        public bool Wind(Vector2 p)
        {
            var c = Cell(p);
            return c.x >= 0 && c.x < W && c.y >= 0 && c.y < H && wind[c.x, c.y];
        }

        public void CellsIn(Rect r, List<Vector2Int> outCells)
        {
            outCells.Clear();
            int x0 = Mathf.FloorToInt(r.xMin + 0.5f), x1 = Mathf.FloorToInt(r.xMax + 0.5f - 1e-4f);
            int y0 = Mathf.FloorToInt(r.yMin + 0.5f), y1 = Mathf.FloorToInt(r.yMax + 0.5f - 1e-4f);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    outCells.Add(new Vector2Int(x, H - 1 - y));
        }

        readonly List<Vector2Int> tmpCells = new List<Vector2Int>();
        public bool RectHitsSolid(Rect r)
        {
            CellsIn(r, tmpCells);
            foreach (var c in tmpCells) if (IsSolid(Get(c))) return true;
            return false;
        }

        // ----------------------------------------------------------------- building
        public static Level Build(string[] map, int pal, Transform parent)
        {
            var go = new GameObject("Level");
            go.transform.SetParent(parent);
            var lv = go.AddComponent<Level>();
            lv.Init(map, pal);
            return lv;
        }

        SpriteRenderer Tile(Vector2 pos, Sprite s, int order, Transform parent, string name = "t")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        void Init(string[] map, int palette)
        {
            pal = palette;
            H = map.Length;
            W = 0;
            foreach (var row in map) W = Mathf.Max(W, row.Length);
            g = new char[W, H];
            wind = new bool[W, H];
            for (int r = 0; r < H; r++)
                for (int x = 0; x < W; x++)
                    g[x, r] = x < map[r].Length ? map[r][x] : '.';

            crateMat = new PhysicsMaterial2D("crate") { friction = 0.6f, bounciness = 0 };

            var tiles = new GameObject("Tiles").transform; tiles.SetParent(transform);
            var solids = new GameObject("Solids"); solids.transform.SetParent(transform);
            var srb = solids.AddComponent<Rigidbody2D>();
            srb.bodyType = RigidbodyType2D.Static;
            var comp = solids.AddComponent<CompositeCollider2D>();
            comp.geometryType = CompositeCollider2D.GeometryType.Outlines;
            comp.generationType = CompositeCollider2D.GenerationType.Manual;

            for (int r = 0; r < H; r++)
            {
                int runStart = -1;
                for (int x = 0; x <= W; x++)
                {
                    char c = x < W ? g[x, r] : '.';
                    bool solid = c == '#' || c == 'F';
                    if (solid && runStart < 0) runStart = x;
                    if (!solid && runStart >= 0)
                    {
                        var bc = solids.AddComponent<BoxCollider2D>();
                        float cx = (runStart + x - 1) * 0.5f;
                        bc.offset = new Vector2(cx, H - 1 - r);
                        bc.size = new Vector2(x - runStart, 1);
                        bc.compositeOperation = Collider2D.CompositeOperation.Merge;
                        runStart = -1;
                    }
                }
            }

            for (int r = 0; r < H; r++)
                for (int x = 0; x < W; x++)
                {
                    char c = g[x, r];
                    var pos = World(x, r);
                    var cell = new Vector2Int(x, r);
                    int variant = (int)(Art.Hash(x, r, 1) * 4);
                    switch (c)
                    {
                        case '#':
                        {
                            bool top = !IsSolidStatic(Get(x, r - 1));
                            Tile(pos, Art.Ground(pal, top, variant), 0, tiles);
                            if (top && r > 0 && Get(x, r - 1) == '.' && Art.Hash(x, r, 3) > 0.62f)
                            {
                                var d = Tile(pos + new Vector2(0, 0.5f), Art.Tuft(pal, (int)(Art.Hash(x, r, 4) * 9)), 1, tiles, "tuft");
                                if (Art.Hash(x, r, 5) > 0.5f) d.flipX = true;
                            }
                            break;
                        }
                        case 'F':
                            fans.Add(cell);
                            fanSr.Add(Tile(pos, Art.Fan(0), 0, tiles, "fan"));
                            break;
                        case 'S':
                        {
                            bool up = IsSolidStatic(Get(x, r + 1)) || !IsSolidStatic(Get(x, r - 1));
                            Tile(pos, Art.Spikes(up), 1, tiles, "spikes");
                            break;
                        }
                        case 'W':
                        case 'L':
                        {
                            bool lava = c == 'L';
                            bool surf = Get(x, r - 1) != c;
                            var sr = Tile(pos, Art.Liquid(lava, surf, 0), 20, tiles, lava ? "lava" : "water");
                            liquids.Add((sr, lava, surf));
                            if (lava && surf) lavaTop.Add(cell);
                            if (lava)
                            {
                                var gl = Tile(pos, Art.Glow(), 19, tiles, "lavaglow");
                                gl.color = new Color(1f, 0.4f, 0.1f, 0.25f);
                                gl.transform.localScale = Vector3.one * 1.2f;
                            }
                            break;
                        }
                        case 'C':
                        case 'B':
                        case 'D':
                        {
                            var sr = Tile(pos, c == 'C' ? Art.Cracked(pal) : c == 'B' ? Art.Barrier() : Art.Door(), 0, tiles, c.ToString());
                            var bc = sr.gameObject.AddComponent<BoxCollider2D>();
                            bc.size = Vector2.one;
                            dyn[cell] = sr.gameObject;
                            if (c == 'D') doors.Add(cell);
                            break;
                        }
                        case 'P':
                        {
                            var sr = Tile(pos, Art.Plate(false), 1, tiles, "plate");
                            dyn[cell] = sr.gameObject;
                            plates.Add(cell);
                            break;
                        }
                        case 'X':
                        {
                            g[x, r] = '.';
                            SpawnCrate(pos);
                            break;
                        }
                        case '@':
                            g[x, r] = '.';
                            spawn = pos;
                            break;
                        case 'K':
                        {
                            var sr = Tile(pos, Art.Checkpoint(false), 2, tiles, "checkpoint");
                            checkpoints[cell] = sr;
                            break;
                        }
                        case 'O':
                        {
                            var sr = Tile(pos, Art.Portal(), 3, tiles, "portal");
                            portal = sr.transform;
                            portalGlow = Tile(pos, Art.Glow(), 2, tiles, "portalglow");
                            portalGlow.color = new Color(0.8f, 0.5f, 1f, 0.6f);
                            portalGlow.transform.localScale = Vector3.one * 3f;
                            break;
                        }
                        default:
                        {
                            var f = FormDef.FromShrine(c);
                            if (f.HasValue)
                            {
                                Tile(pos, Art.Pedestal(), 2, tiles, "shrine");
                                var def = FormDef.Get(f.Value);
                                var glow = Tile(pos + new Vector2(0, 0.35f), Art.Glow(), 2, tiles, "shrineglow");
                                var gc = def.color; gc.a = 0.7f; glow.color = gc;
                                glow.transform.localScale = Vector3.one * 1.8f;
                                var icon = Tile(pos + new Vector2(0, 0.35f), Art.FormSprite(f.Value), 4, tiles, "shrineicon");
                                icon.transform.localScale = Vector3.one * (f.Value == Form.Boulder ? 0.35f : 0.7f);
                                shrines.Add((icon.transform, glow, pos.y + 0.35f));
                            }
                            break;
                        }
                    }
                }

            comp.GenerateGeometry();

            // wind columns above fans
            foreach (var f in fans)
            {
                for (int k = 1; k <= WindLength; k++)
                {
                    int r = f.y - k;
                    if (r < 0 || IsSolidStatic(g[f.x, r])) break;
                    wind[f.x, r] = true;
                }
            }
        }

        static bool IsSolidStatic(char c) => c == '#' || c == 'C' || c == 'B' || c == 'D' || c == 'F';

        void SpawnCrate(Vector2 pos)
        {
            var go = new GameObject("Crate");
            go.transform.SetParent(transform);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Crate();
            sr.sortingOrder = 2;
            go.transform.localScale = new Vector3(0.92f, 0.92f, 1);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = 3f;
            rb.gravityScale = 2f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
            bc.sharedMaterial = crateMat;
            var cr = go.AddComponent<Crate>();
            cr.level = this;
            crates.Add(cr);
        }

        // ----------------------------------------------------------------- interactions
        public bool TryCrack(Vector2Int cell)
        {
            if (Get(cell) != 'C' || pendingSet.Contains(cell)) return false;
            Flood(cell, 'C', 0.12f, 0.06f);
            Sfx.Play("crumble", 0.8f);
            return true;
        }

        public bool TryBurn(Vector2Int cell)
        {
            if (Get(cell) != 'B' || pendingSet.Contains(cell)) return false;
            Flood(cell, 'B', 0.05f, 0.12f);
            Sfx.Play("burn", 0.9f);
            return true;
        }

        void Flood(Vector2Int start, char kind, float baseDelay, float step)
        {
            var q = new Queue<(Vector2Int, int)>();
            q.Enqueue((start, 0));
            pendingSet.Add(start);
            while (q.Count > 0)
            {
                var (c, d) = q.Dequeue();
                pending.Add((c, Time.time + baseDelay + d * step, kind));
                foreach (var n in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var nc = c + n;
                    if (Get(nc) == kind && !pendingSet.Contains(nc)) { pendingSet.Add(nc); q.Enqueue((nc, d + 1)); }
                }
            }
        }

        public bool IsCheckpoint(Vector2Int c) => checkpoints.ContainsKey(c);

        public void ActivateCheckpoint(Vector2Int c)
        {
            foreach (var kv in checkpoints) kv.Value.sprite = Art.Checkpoint(kv.Key == c);
        }

        bool PlatePressed(Vector2Int plate, Player player)
        {
            var center = World(plate.x, plate.y);
            foreach (var cr in crates)
                if (cr != null && Mathf.Abs(cr.transform.position.x - center.x) < 0.45f && Mathf.Abs(cr.transform.position.y - center.y) < 0.6f)
                    return true;
            if (player != null && player.form == Form.Boulder && !player.dead)
            {
                var b = player.Bounds;
                if (b.xMin < center.x + 0.3f && b.xMax > center.x - 0.3f && b.yMin < center.y + 0.65f && b.yMax > center.y - 0.5f)
                    return true;
            }
            return false;
        }

        bool DoorsOccupied(Player player)
        {
            foreach (var d in doors)
            {
                var p = World(d.x, d.y);
                var r = new Rect(p.x - 0.5f, p.y - 0.5f, 1, 1);
                if (player != null && !player.dead && r.Overlaps(player.Bounds)) return true;
                foreach (var cr in crates)
                    if (cr != null && Vector2.Distance(cr.transform.position, p) < 0.95f) return true;
            }
            return false;
        }

        void SetDoors(bool open)
        {
            doorsOpen = open;
            foreach (var d in doors)
            {
                if (!dyn.TryGetValue(d, out var go) || go == null) continue;
                go.GetComponent<Collider2D>().enabled = !open;
                go.GetComponent<SpriteRenderer>().color = open ? new Color(1, 1, 1, 0.18f) : Color.white;
            }
            Sfx.Play("door", 0.8f);
        }

        void FixedUpdate()
        {
            if (plates.Count == 0) return;
            var player = Game.I != null ? Game.I.player : null;
            bool any = false;
            foreach (var p in plates)
            {
                bool pr = PlatePressed(p, player);
                any |= pr;
                if (dyn.TryGetValue(p, out var go) && go != null) go.GetComponent<SpriteRenderer>().sprite = Art.Plate(pr);
            }
            if (any && !doorsOpen) SetDoors(true);
            else if (!any && doorsOpen && !DoorsOccupied(player)) SetDoors(false);
        }

        void Update()
        {
            float t = Time.time;
            // timed crumble / burn
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var (cell, when, kind) = pending[i];
                if (t < when) { if (kind == 'B' && Random.value < 0.3f) Fx.Emit(World(cell.x, cell.y) + Random.insideUnitCircle * 0.4f, new Vector2(0, 2), new Color(1, Random.Range(0.4f, 0.8f), 0.1f), 0.5f, -1, 0.15f); continue; }
                pending.RemoveAt(i);
                pendingSet.Remove(cell);
                g[cell.x, cell.y] = '.';
                if (dyn.TryGetValue(cell, out var go) && go != null) Destroy(go);
                dyn.Remove(cell);
                var pos = World(cell.x, cell.y);
                if (kind == 'C') Fx.Burst(pos, Color.Lerp(Art.Palettes[pal].dirtLight, Color.gray, 0.5f), 12, 4f, 0.8f, 18f, 0.18f, 0.4f);
                else { Fx.Burst(pos, new Color(1f, 0.55f, 0.1f), 14, 3f, 0.7f, -4f, 0.16f, 0.4f); Fx.Burst(pos, new Color(0.25f, 0.2f, 0.2f, 0.8f), 6, 1.5f, 1.2f, -2f, 0.2f, 0.4f); }
            }

            animT += Time.deltaTime;
            if (animT > 0.25f)
            {
                animT = 0;
                animFrame = 1 - animFrame;
                foreach (var l in liquids) if (l.sr != null) l.sr.sprite = Art.Liquid(l.lava, l.surf, animFrame);
                foreach (var f in fanSr) f.sprite = Art.Fan(animFrame);
            }

            // ambient particles
            foreach (var f in fans)
                if (Random.value < 0.25f)
                {
                    var p = World(f.x, f.y) + new Vector2(Random.Range(-0.45f, 0.45f), 0.5f);
                    Fx.Emit(p, new Vector2(0, Random.Range(5f, 8f)), new Color(0.85f, 0.95f, 1f, 0.5f), WindLength / 7f, 0, 0.08f);
                }
            foreach (var l in lavaTop)
                if (Random.value < 0.015f)
                    Fx.Emit(World(l.x, l.y) + new Vector2(Random.Range(-0.4f, 0.4f), 0.3f), new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(2f, 4f)), new Color(1f, 0.7f, 0.2f), 0.8f, 6f, 0.12f);

            foreach (var s in shrines)
            {
                s.icon.position = new Vector3(s.icon.position.x, s.baseY + Mathf.Sin(t * 2.5f + s.icon.position.x) * 0.1f, 0);
                var c = s.glow.color; c.a = 0.45f + Mathf.Sin(t * 3 + s.icon.position.x) * 0.2f; s.glow.color = c;
            }
            if (portal != null)
            {
                portal.Rotate(0, 0, -120 * Time.deltaTime);
                float sc = 1f + Mathf.Sin(t * 3) * 0.05f;
                portal.localScale = new Vector3(sc, sc, 1);
                if (Random.value < 0.3f) Fx.Emit((Vector2)portal.position + Random.insideUnitCircle * 1.2f, Vector2.zero, new Color(0.9f, 0.7f, 1f), 0.6f, -2f, 0.1f);
            }
        }
    }

    /// <summary>Crates only slide when a Boulder leans on them; everyone else finds them immovable.</summary>
    public class Crate : MonoBehaviour
    {
        public Level level;
        Rigidbody2D rb;

        void Awake() { rb = GetComponent<Rigidbody2D>(); }

        void FixedUpdate()
        {
            var p = Game.I != null ? Game.I.player : null;
            var pos = (Vector2)transform.position;
            bool pushing = false;
            if (p != null && p.form == Form.Boulder && !p.dead)
            {
                var b = p.Bounds;
                float dy = pos.y - b.center.y;
                float overlapY = (b.height + 1f) * 0.5f - Mathf.Abs(dy);
                float gapX = Mathf.Abs(pos.x - b.center.x) - (b.width + 1f) * 0.5f;
                pushing = overlapY > 0.25f && gapX < 0.12f && Mathf.Sign(pos.x - b.center.x) == Mathf.Sign(p.Rb.linearVelocity.x);
            }

            // Drop cleanly into one-tile holes instead of skimming across them.
            var cell = level.Cell(pos);
            var below = level.Get(cell.x, cell.y + 1);
            int cellX = cell.x;
            if (!level.IsSolid(below) && Mathf.Abs(pos.x - cellX) < 0.3f)
            {
                bool leftSolid = level.IsSolid(level.Get(cell.x - 1, cell.y + 1));
                bool rightSolid = level.IsSolid(level.Get(cell.x + 1, cell.y + 1));
                if (leftSolid && rightSolid)
                {
                    rb.position = new Vector2(cellX, rb.position.y);
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    pushing = false;
                }
            }

            rb.constraints = pushing
                ? RigidbodyConstraints2D.FreezeRotation
                : RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
        }
    }
}
