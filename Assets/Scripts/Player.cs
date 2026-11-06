using System.Collections.Generic;
using UnityEngine;

namespace Shiftling
{
    public class Player : MonoBehaviour
    {
        public Form form = Form.Wisp;
        public Form lastForm = Form.Wisp;
        public bool dead;
        public float moisture = 1f;   // Frog
        public float stamina = 1f;    // Bat
        public bool swimming;
        public bool grounded;

        public Rigidbody2D Rb { get; private set; }
        BoxCollider2D col;
        Transform vis;
        SpriteRenderer sr, glow;
        ContactFilter2D solidFilter;
        readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        readonly List<Collider2D> overlaps = new List<Collider2D>();
        readonly List<Vector2Int> cells = new List<Vector2Int>();

        float coyote, jumpBuffer, flapTimer, shiftCd, squash, roll, animT, flash, trailT;
        int airJumps, frame;
        bool rising;
        float facing = 1;
        float prevVy;

        const float Edge = 0.04f;

        FormDef Def => FormDef.Get(form);
        Level Lv => Game.I.level;

        public Rect Bounds
        {
            get
            {
                var s = Def.size;
                var p = Rb != null ? Rb.position : (Vector2)transform.position;
                return new Rect(p - s * 0.5f, s);
            }
        }

        public static Player Create(Transform parent)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent);
            var p = go.AddComponent<Player>();
            p.Init();
            return p;
        }

        void Init()
        {
            Rb = gameObject.AddComponent<Rigidbody2D>();
            Rb.freezeRotation = true;
            Rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            Rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            col = gameObject.AddComponent<BoxCollider2D>();
            col.edgeRadius = Edge;
            col.sharedMaterial = new PhysicsMaterial2D("player") { friction = 0f, bounciness = 0f };

            var g = new GameObject("Glow");
            g.transform.SetParent(transform, false);
            glow = g.AddComponent<SpriteRenderer>();
            glow.sprite = Art.Glow();
            glow.sortingOrder = 9;

            var v = new GameObject("Visual");
            v.transform.SetParent(transform, false);
            vis = v.transform;
            sr = v.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;

            solidFilter = new ContactFilter2D();
            solidFilter.useTriggers = false;
            ApplyForm(form, transform.position);
        }

        // ------------------------------------------------------------------ shifting
        public void Respawn(Vector2 pos, Form f)
        {
            dead = false;
            Rb.simulated = true;
            Rb.linearVelocity = Vector2.zero;
            vis.gameObject.SetActive(true);
            glow.gameObject.SetActive(true);
            ApplyForm(f, pos);
            lastForm = f;
            Fx.Burst(pos, Def.color, 20, 4f, 0.6f);
        }

        void ApplyForm(Form f, Vector2 center)
        {
            form = f;
            var d = Def;
            col.size = d.size - Vector2.one * (Edge * 2);
            Rb.mass = d.mass;
            Rb.gravityScale = d.gravity;
            Rb.position = center;
            transform.position = center;
            sr.sprite = Art.FormSprite(f);
            vis.localRotation = Quaternion.identity;
            roll = 0;
            float yOff = f switch { Form.Wisp => 0.08f, Form.Frog => 0.12f, Form.Ember => 0.08f, _ => 0f };
            vis.localPosition = new Vector3(0, yOff, 0);
            var gc = d.color;
            gc.a = f == Form.Wisp || f == Form.Ember ? 0.45f : 0.18f;
            glow.color = gc;
            glow.transform.localScale = Vector3.one * (f == Form.Boulder ? 4f : 2.4f);
            airJumps = f == Form.Wisp ? 1 : 0;
            if (f == Form.Frog) moisture = 1f;
            if (f == Form.Bat) stamina = Mathf.Max(stamina, 0.35f);
            flash = 1f;
        }

        public bool TryShift(Form f, bool forced = false, string forcedMsg = null)
        {
            if (dead || f == form) return false;
            if (!forced && !Game.I.IsUnlocked(f))
            {
                Game.I.Toast("You haven't learned that shape yet");
                Sfx.Play("nope");
                return false;
            }
            if (!forced && shiftCd > 0) return false;

            var nd = FormDef.Get(f);
            var cur = Bounds;
            float[] offs = { 0, 0.3f, -0.3f, 0.6f, -0.6f, 0.95f, -0.95f };
            Vector2? spot = null;
            foreach (var bottomAligned in new[] { true, false })
            {
                foreach (var o in offs)
                {
                    float cy = bottomAligned ? cur.yMin + nd.size.y * 0.5f + 0.02f : cur.center.y;
                    var c = new Vector2(cur.center.x + o, cy);
                    if (Fits(c, nd.size)) { spot = c; break; }
                }
                if (spot.HasValue) break;
            }

            if (!spot.HasValue)
            {
                if (!forced)
                {
                    Game.I.Toast($"No room to become a {nd.name}!");
                    Sfx.Play("nope");
                    return false;
                }
                spot = Rb.position;
            }

            lastForm = form;
            var keepV = Rb.linearVelocity;
            ApplyForm(f, spot.Value);
            Rb.linearVelocity = new Vector2(keepV.x * 0.5f, Mathf.Max(keepV.y * 0.5f, f == Form.Boulder ? -5 : -3));
            shiftCd = 0.25f;
            squash = -0.35f;
            Fx.Burst(spot.Value, nd.color, 26, 6f, 0.55f, 0, 0.16f, nd.size.x * 0.4f);
            Fx.Burst(spot.Value, Color.white, 10, 3f, 0.35f, 0, 0.12f, 0.2f);
            Sfx.Play("shift", 0.9f, 0.1f);
            Game.I.Shake(0.12f);
            if (forcedMsg != null) Game.I.Toast(forcedMsg);
            return true;
        }

        bool Fits(Vector2 c, Vector2 size)
        {
            var r = new Rect(c - size * 0.5f + Vector2.one * 0.03f, size - Vector2.one * 0.06f);
            if (Lv.RectHitsSolid(r)) return false;
            overlaps.Clear();
            Physics2D.OverlapBox(c, size - Vector2.one * 0.06f, 0, solidFilter, overlaps);
            foreach (var o in overlaps) if (o != col && !o.isTrigger) return false;
            return true;
        }

        public void Die(string reason)
        {
            if (dead) return;
            dead = true;
            Fx.Burst(Rb.position, Def.color, 36, 9f, 0.9f, 10f, 0.2f, 0.3f);
            Fx.Burst(Rb.position, Color.white, 12, 5f, 0.5f, 0, 0.14f, 0.2f);
            Sfx.Play("die");
            Game.I.Shake(0.35f);
            Rb.simulated = false;
            vis.gameObject.SetActive(false);
            glow.gameObject.SetActive(false);
            Game.I.OnPlayerDied(reason);
        }

        // ------------------------------------------------------------------ input
        void Update()
        {
            if (dead || Game.I == null || !Game.I.Playing) return;
            float dt = Time.deltaTime;
            if (Controls.JumpDown) jumpBuffer = 0.13f;
            jumpBuffer -= dt;
            shiftCd -= dt;

            int k = Controls.FormKey;
            if (k >= 0) TryShift((Form)k);
            if (Controls.CycleNext) Cycle(1);
            if (Controls.CyclePrev) Cycle(-1);
            if (Controls.SwapLast) TryShift(lastForm);

            Animate(dt);
        }

        void Cycle(int dir)
        {
            int n = FormDef.All.Length;
            for (int i = 1; i < n; i++)
            {
                var f = (Form)(((int)form + dir * i + n * 2) % n);
                if (Game.I.IsUnlocked(f)) { TryShift(f); return; }
            }
            Sfx.Play("nope");
        }

        // ------------------------------------------------------------------ physics
        bool CheckGround()
        {
            int n = Rb.Cast(Vector2.down, solidFilter, hits, 0.06f);
            for (int i = 0; i < n; i++)
                if (hits[i].normal.y > 0.55f && hits[i].collider != col) return true;
            return false;
        }

        void FixedUpdate()
        {
            if (dead || Game.I == null || !Game.I.Playing) return;
            float dt = Time.fixedDeltaTime;
            var d = Def;
            var v = Rb.linearVelocity;
            var pos = Rb.position;
            float inX = Controls.X;
            bool jumpHeld = Controls.JumpHeld;
            if (inX != 0) facing = Mathf.Sign(inX);

            char center = Lv.At(pos);
            swimming = (form == Form.Frog && center == 'W') || (form == Form.Ember && center == 'L');
            bool inWind = Lv.Wind(pos) || Lv.Wind(pos + Vector2.down * d.size.y * 0.45f);

            bool wasGrounded = grounded;
            grounded = CheckGround();
            if (grounded)
            {
                coyote = 0.1f;
                if (form == Form.Wisp) airJumps = 1;
                if (form == Form.Bat) stamina = Mathf.Min(1, stamina + dt * 2.5f);
                if (!wasGrounded) OnLand(prevVy);
            }
            else coyote -= dt;

            float accel = form == Form.Boulder ? 22f : grounded ? 80f : 50f;

            if (swimming)
            {
                Rb.gravityScale = 0.1f;
                float targetY = jumpHeld ? 5.5f : Controls.DownHeld ? -5.5f : -0.6f;
                v.y = Mathf.MoveTowards(v.y, targetY, 30f * dt);
                v.x = Mathf.MoveTowards(v.x, inX * d.speed * 0.85f, 40f * dt);
                bool nearSurface = !Lv.IsLiquid(Lv.At(pos + Vector2.up * 0.55f));
                if (jumpBuffer > 0 && nearSurface)
                {
                    v.y = d.jumpVel * 0.9f;
                    jumpBuffer = 0;
                    rising = true;
                    Sfx.Play("splash", 0.6f);
                    Fx.Burst(pos + Vector2.up * 0.3f, form == Form.Frog ? new Color(0.6f, 0.85f, 1f) : new Color(1f, 0.6f, 0.2f), 10, 4f, 0.5f, 15f, 0.14f);
                }
                if (Random.value < 0.05f) Fx.Emit(pos, new Vector2(0, 1.5f), new Color(1, 1, 1, 0.5f), 0.8f, -1, 0.08f);
            }
            else
            {
                Rb.gravityScale = d.gravity;
                v.x = Mathf.MoveTowards(v.x, inX * d.speed, accel * dt);

                if (form == Form.Bat)
                {
                    flapTimer -= dt;
                    if (jumpHeld && stamina > 0)
                    {
                        stamina -= dt / 3.6f;
                        if (flapTimer <= 0 && v.y < d.jumpVel * 0.7f)
                        {
                            v.y = d.jumpVel;
                            flapTimer = 0.2f;
                            Sfx.Play("flap", 0.5f, 0.15f);
                            frame = 1 - frame;
                        }
                    }
                    v.y = Mathf.Max(v.y, -4f);
                }
                else if (jumpBuffer > 0)
                {
                    if (coyote > 0 && d.jumpVel > 0)
                    {
                        v.y = d.jumpVel;
                        jumpBuffer = 0; coyote = 0; rising = true;
                        squash = -0.25f;
                        Sfx.Play(form == Form.Frog ? "frogjump" : "jump", 0.7f);
                        Fx.Burst(pos + Vector2.down * d.size.y * 0.5f, new Color(1, 1, 1, 0.6f), 6, 2.5f, 0.3f, 0, 0.12f);
                    }
                    else if (airJumps > 0)
                    {
                        v.y = 10.5f;
                        airJumps--; jumpBuffer = 0; rising = true;
                        squash = -0.25f;
                        Sfx.Play("djump", 0.7f);
                        Fx.Burst(pos + Vector2.down * 0.3f, d.color, 14, 4f, 0.4f, 0, 0.14f);
                    }
                }

                // variable jump height
                if (rising && !jumpHeld && v.y > 0 && form != Form.Bat) { v.y *= 0.5f; rising = false; }
                if (v.y <= 0) rising = false;

                if (inWind && form != Form.Boulder)
                {
                    float cap = form == Form.Bat ? 19f : 7f;
                    float acc = form == Form.Bat ? 110f : 75f;
                    if (v.y < cap) v.y = Mathf.Min(v.y + acc * dt, cap);
                    if (Random.value < 0.3f) Fx.Emit(pos + Random.insideUnitCircle * 0.3f, new Vector2(0, 6), new Color(1, 1, 1, 0.5f), 0.3f, 0, 0.08f);
                }
                v.y = Mathf.Max(v.y, -22f);
            }

            Rb.linearVelocity = v;
            prevVy = v.y;

            // frog moisture
            if (form == Form.Frog)
            {
                if (TouchesChar('W', 0.0f)) moisture = Mathf.Min(1, moisture + dt * 1.5f);
                else moisture -= dt / 7f;
                if (moisture <= 0) { moisture = 1; TryShift(Form.Wisp, true, "The frog dried out!"); }
            }

            if (form == Form.Boulder && grounded)
            {
                var b = Bounds;
                for (float x = b.xMin + 0.15f; x <= b.xMax - 0.15f; x += 0.4f)
                    Lv.TryCrack(Lv.Cell(new Vector2(x, b.yMin - 0.25f)));
            }
            if (form == Form.Ember)
            {
                var b = Bounds;
                var er = new Rect(b.xMin - 0.15f, b.yMin - 0.1f, b.width + 0.3f, b.height + 0.2f);
                Lv.CellsIn(er, cells);
                foreach (var c in cells) Lv.TryBurn(c);
            }

            CheckHazards();
            if (!dead) CheckPickups();
        }

        bool TouchesChar(char ch, float shrink)
        {
            var b = Bounds;
            var r = new Rect(b.xMin + shrink, b.yMin + shrink, b.width - shrink * 2, b.height - shrink * 2);
            Lv.CellsIn(r, cells);
            foreach (var c in cells) if (Lv.Get(c) == ch) return true;
            return false;
        }

        void OnLand(float vy)
        {
            if (vy < -6f)
            {
                squash = Mathf.Clamp(-vy / 50f, 0.1f, 0.35f);
                var foot = Rb.position + Vector2.down * Def.size.y * 0.5f;
                if (form == Form.Boulder)
                {
                    Sfx.Play("thud", 0.9f);
                    Game.I.Shake(0.25f);
                    Fx.Burst(foot, new Color(0.8f, 0.75f, 0.7f, 0.8f), 16, 5f, 0.5f, 8f, 0.18f, 0.6f);
                }
                else
                {
                    Sfx.Play("land", 0.5f);
                    Fx.Burst(foot, new Color(1, 1, 1, 0.5f), 5, 2f, 0.3f, 0, 0.1f);
                }
            }
        }

        void CheckHazards()
        {
            var b = Bounds;
            if (b.yMax < -1.5f) { Die("You fell into the void!"); return; }
            float s = 0.14f;
            var r = new Rect(b.xMin + s, b.yMin + s, b.width - s * 2, b.height - s * 2);
            Lv.CellsIn(r, cells);
            foreach (var c in cells)
            {
                switch (Lv.Get(c))
                {
                    case 'S':
                        if (form != Form.Boulder) { Die("Spiked! Only a Boulder can shrug that off."); return; }
                        break;
                    case 'L':
                        if (form != Form.Ember) { Die(form == Form.Boulder ? "Melted! Only an Ember can swim in lava." : "Burned! Only an Ember can swim in lava."); return; }
                        break;
                    case 'W':
                        switch (form)
                        {
                            case Form.Boulder: Die("Sank like a stone!"); return;
                            case Form.Ember: Die("Snuffed out by water!"); return;
                            case Form.Bat: Die("Soaked! Bats can't swim."); return;
                            case Form.Wisp: Die("Fizzled out! Wisps hate water."); return;
                        }
                        break;
                }
            }
        }

        void CheckPickups()
        {
            Lv.CellsIn(Bounds, cells);
            foreach (var c in cells)
            {
                char ch = Lv.Get(c);
                if (ch == 'O') { Game.I.LevelComplete(); return; }
                if (ch == 'K') Game.I.ReachCheckpoint(c);
                // shrines also catch you while you pass through the two tiles above them
                for (int k = 0; k <= 2; k++)
                {
                    var f = FormDef.FromShrine(k == 0 ? ch : Lv.Get(c.x, c.y + k));
                    if (f.HasValue) { Game.I.TouchShrine(f.Value); break; }
                }
            }
        }

        // ------------------------------------------------------------------ visuals
        void Animate(float dt)
        {
            var v = Rb.linearVelocity;
            animT += dt;
            squash = Mathf.MoveTowards(squash, 0, dt * 1.6f);
            flash = Mathf.MoveTowards(flash, 0, dt * 4f);

            switch (form)
            {
                case Form.Wisp:
                    if (animT > 0.2f) { animT = 0; frame = 1 - frame; }
                    sr.sprite = Art.FormSprite(form, frame);
                    vis.localPosition = new Vector3(0, 0.08f + Mathf.Sin(Time.time * 6) * 0.03f, 0);
                    break;
                case Form.Frog:
                    sr.sprite = Art.FormSprite(form, grounded && !swimming ? 0 : 1);
                    break;
                case Form.Bat:
                    if (grounded) frame = 0;
                    else if (!Controls.JumpHeld || stamina <= 0) { if (animT > 0.25f) { animT = 0; frame = 1 - frame; } }
                    sr.sprite = Art.FormSprite(form, frame);
                    break;
                case Form.Ember:
                    if (animT > 0.12f) { animT = 0; frame = 1 - frame; }
                    sr.sprite = Art.FormSprite(form, frame);
                    break;
                case Form.Boulder:
                    roll -= v.x * dt / 0.9f * Mathf.Rad2Deg;
                    vis.localRotation = Quaternion.Euler(0, 0, roll);
                    break;
            }

            float sx = 1 + squash, sy = 1 - squash;
            if (form == Form.Boulder) vis.localScale = new Vector3(sx, sy, 1);
            else vis.localScale = new Vector3(sx * facing, sy, 1);

            sr.color = Color.Lerp(Color.white, new Color(1, 1, 1, 1) * 3f, flash);
            if (form == Form.Frog && moisture < 0.3f) sr.color = Color.Lerp(new Color(0.8f, 0.75f, 0.5f), Color.white, moisture / 0.3f);
            if (form == Form.Bat && stamina <= 0.01f && !grounded) sr.color = new Color(0.7f, 0.7f, 0.8f);

            var gc = glow.color;
            gc.a = (form == Form.Wisp || form == Form.Ember ? 0.4f : 0.15f) + Mathf.Sin(Time.time * 5) * 0.05f + flash * 0.4f;
            glow.color = gc;

            trailT -= dt;
            if (trailT <= 0)
            {
                trailT = 0.05f;
                var p = Rb.position;
                if (form == Form.Ember && !swimming)
                    Fx.Emit(p + new Vector2(Random.Range(-0.2f, 0.2f), 0.2f), new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1.5f, 3f)), new Color(1f, Random.Range(0.4f, 0.85f), 0.1f), 0.45f, -2f, 0.12f);
                else if (form == Form.Wisp && Random.value < 0.6f)
                    Fx.Emit(p + Random.insideUnitCircle * 0.25f, -v * 0.1f + Vector2.up * 0.3f, new Color(0.6f, 1f, 1f, 0.7f), 0.5f, 0, 0.09f);
                else if (form == Form.Frog && moisture < 0.35f && Random.value < 0.3f)
                    Fx.Emit(p + Random.insideUnitCircle * 0.3f, Vector2.up * 0.5f, new Color(0.9f, 0.85f, 0.6f, 0.6f), 0.5f, 0, 0.08f);
            }
        }
    }
}
