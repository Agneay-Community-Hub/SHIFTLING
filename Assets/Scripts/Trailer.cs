using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Shiftling
{
    /// <summary>
    /// Records a gameplay trailer. Launch a player build with:
    ///   Shiftling.exe -trailer &lt;outDir&gt; -screen-width 1280 -screen-height 720 -screen-fullscreen 0
    /// Writes numbered JPG frames at a locked 30 fps plus soundtrack.wav (music + every sound effect,
    /// mixed offline in sync), then quits. Tools/make_trailer.ps1 turns them into an MP4.
    /// </summary>
    public class Trailer : MonoBehaviour
    {
        const int FPS = 30;
        string dir;
        int frame;
        bool recording;
        readonly List<(float t, string name, float vol, float pitch)> sounds = new List<(float, string, float, float)>();

        // overlay state
        string capTitle, capSub; float capStart, capEnd;
        string cardTitle, cardSub, cardSmall; bool cardLogo; float cardAlpha;
        float fade = 1f;   // black overlay alpha
        float fadeTarget = 0f;
        GUIStyle sLogo, sCap, sSub, sSmall;

        Game G => Game.I;
        Player P => Game.I.player;
        float T => frame / (float)FPS;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-trailer");
            dir = i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : "Trailer";
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "f*.jpg")) File.Delete(f);

            Application.runInBackground = true;
            Time.captureFramerate = FPS;
            AudioListener.volume = 0f;
            Sfx.OnPlay = (n, v, p) => { if (recording) sounds.Add((T, n, v, p)); };
            G.HideHud = true;
            StartCoroutine(Director());
        }

        // ------------------------------------------------------------------ helpers
        IEnumerator Sec(float s)
        {
            int target = frame + Mathf.RoundToInt(s * FPS);
            while (frame < target) yield return null;
        }

        IEnumerator Until(float maxSec, Func<bool> done)
        {
            int target = frame + Mathf.RoundToInt(maxSec * FPS);
            while (frame < target && !done()) yield return null;
        }

        void Caption(string title, string sub, float dur)
        {
            capTitle = title; capSub = sub; capStart = T; capEnd = T + dur;
        }

        IEnumerator FadeOut(float s = 0.35f) { fadeTarget = 1; yield return Sec(s); }

        IEnumerator Shot(int level, int x, int row, Form f, bool unlockAll = true)
        {
            Controls.SimReset();
            G.StartLevel(level);
            if (unlockAll) G.UnlockAll();
            yield return null;
            G.PlaceAt(x, row, f);
            G.SnapCamera();
            yield return Sec(0.25f);
            G.SnapCamera();
            fadeTarget = 0;
        }

        /// <summary>
        /// A small "autopilot": runs right, jumping at walls and over hazards,
        /// using the Wisp's double jump when it is about to land somewhere nasty.
        /// </summary>
        IEnumerator AutoRun(float maxSec, Func<bool> done)
        {
            Controls.Sim = true;
            Controls.SimX = 1;
            int target = frame + Mathf.RoundToInt(maxSec * FPS);
            float holdT = -1;        // >= 0 while a jump press is being held
            bool usedDouble = false, lastWant = false;
            while (frame < target && !done())
            {
                var lv = G.level;
                var b = P.Bounds;
                float feet = b.yMin + 0.3f;
                bool wallAhead = lv.IsSolid(lv.At(new Vector2(b.xMax + 0.45f, feet))) || lv.IsSolid(lv.At(new Vector2(b.xMax + 0.45f, feet + 1f)));
                bool danger = false;
                for (float dx = 0.3f; dx < 2.6f; dx += 0.5f)
                {
                    char c1 = lv.At(new Vector2(b.xMax + dx, feet - 0.1f)), c2 = lv.At(new Vector2(b.xMax + dx, feet - 1.1f));
                    if (c1 == 'S' || c1 == 'W' || c1 == 'L' || c2 == 'S' || c2 == 'W' || c2 == 'L') danger = true;
                }
                char under = lv.At(new Vector2(b.center.x, b.yMin - 0.6f)), under2 = lv.At(new Vector2(b.center.x, b.yMin - 1.6f));
                bool badBelow = under == 'S' || under == 'W' || under == 'L' || under2 == 'S' || under2 == 'W' || under2 == 'L';

                if (P.grounded) usedDouble = false;
                if (holdT < 0 && !lastWant)
                {
                    if (P.grounded && (wallAhead || danger)) holdT = 0;
                    else if (!P.grounded && !usedDouble && P.Rb.linearVelocity.y < 0.5f && (wallAhead || badBelow))
                    { holdT = 0; usedDouble = true; }
                }
                bool want = holdT >= 0;
                if (holdT >= 0) { holdT += Time.deltaTime; if (holdT >= 0.42f) holdT = -1; }
                Controls.SimJump(want);
                lastWant = want;
                yield return null;
            }
            Controls.SimReset();
        }

        /// <summary>Hold right, tap jump on a rhythm while allowJump() is true (same pattern the tests use).</summary>
        IEnumerator RhythmRun(float maxSec, Func<bool> done, float every, Func<bool> allowJump = null)
        {
            Controls.Sim = true;
            Controls.SimX = 1;
            int target = frame + Mathf.RoundToInt(maxSec * FPS);
            float jt = 0;
            while (frame < target && !done())
            {
                jt += Time.deltaTime;
                bool may = allowJump == null || allowJump();
                Controls.SimJump(may && jt % every < every * 0.6f);
                yield return null;
            }
            Controls.SimReset();
        }

        // ------------------------------------------------------------------ the trailer
        IEnumerator Director()
        {
            Controls.Sim = true;
            yield return null;
            recording = true;
            StartCoroutine(Capture());

            // 1. Logo over a drifting title background
            G.ShowTitle(4);
            cardLogo = true; cardTitle = "SHIFTLING"; cardSub = "a shapeshifting adventure"; cardSmall = null;
            fadeTarget = 0;
            yield return Sec(3.6f);
            yield return FadeOut();
            cardLogo = false; cardTitle = null;

            // 2. The Wisp
            yield return Shot(0, 3, 9, Form.Wisp, false);
            Caption("You are a Wisp.", "small, quick, and fragile", 3.6f);
            yield return AutoRun(8f, () => P.grounded && P.Rb.position.x > 30.5f);
            Caption("Touch a shrine...", "and learn a new shape", 2.8f);
            yield return AutoRun(3f, () => P.form == Form.Frog);
            Controls.Sim = true; Controls.SimX = 1;
            yield return Until(2f, () => P.form == Form.Frog);
            Controls.SimX = 0;
            yield return Sec(1.0f);
            yield return AutoRun(1.2f, () => false);
            yield return FadeOut();

            // 3. Boulder: shrine, spikes, crushing floors
            yield return Shot(1, 4, 10, Form.Wisp, false);
            Controls.SimX = 1;
            yield return Until(3f, () => P.form == Form.Boulder);
            Caption("...then SHIFT into it.", "a Boulder shrugs off spikes", 3.5f);
            yield return RhythmRun(10f, () => P.Rb.position.x > 36f, 10f, () => false);
            Caption("and crushes weak floors", "", 3f);
            yield return RhythmRun(7f, () => G.level.doorsOpen && P.Rb.position.x > 53f, 10f, () => false);
            yield return Sec(0.3f);
            yield return FadeOut();

            // 4. Bat
            yield return Shot(2, 3, 19, Form.Bat);
            Caption("Take to the skies as a Bat", "", 3.4f);
            Controls.SimJump(true);
            yield return Sec(2.0f);
            Controls.SimX = 1;
            yield return Until(4f, () => P.Rb.position.x > 24f);
            Controls.SimReset();
            yield return FadeOut();

            // 5. Ember
            yield return Shot(3, 3, 11, Form.Ember);
            Caption("Burn through wood as an Ember", "and swim through lava", 4.5f);
            yield return RhythmRun(9f, () => P.Rb.position.x > 35f, 0.3f);
            yield return FadeOut();

            // 6. Frog
            yield return Shot(0, 39, 9, Form.Frog);
            Caption("Dive deep as a Frog", "", 3.5f);
            yield return RhythmRun(6.5f, () => P.Rb.position.x > 55f, 0.4f, () => P.Rb.position.x < 44.5f || P.Rb.position.x > 51f);
            yield return Sec(0.4f);
            yield return FadeOut();

            // 7. Weaknesses
            yield return Shot(2, 27, 12, Form.Bat);
            Caption("But every shape has a weakness.", "bats are light - fans fling them", 3.6f);
            Controls.SimX = 1;
            yield return Until(4f, () => P.dead);
            Controls.SimReset();
            yield return Sec(0.5f);
            yield return FadeOut(0.3f);
            yield return Shot(0, 42, 7, Form.Wisp);
            Caption("", "wisps fizzle in water", 2.4f);
            yield return RhythmRun(3f, () => P.dead, 0.4f);
            yield return Sec(0.5f);
            yield return FadeOut(0.3f);

            // 8. Shift wisely - ride the wind to the portal
            yield return Shot(4, 106, 16, Form.Wisp);
            Caption("Shift wisely.", "", 3.5f);
            Controls.SimX = 1;
            yield return Until(9f, () => G.IsLevelComplete);
            Controls.SimReset();
            yield return Sec(1.0f);
            yield return FadeOut(0.5f);

            // 9. End card
            G.ShowTitle(2);
            cardLogo = true; cardTitle = "SHIFTLING";
            cardSub = "5 shapes  -  5 levels  -  one key: SHIFT";
            cardSmall = "GitHub Game Off 2026   -   Play free on itch.io";
            fadeTarget = 0;
            yield return Sec(4.8f);
            yield return FadeOut(0.6f);
            yield return Sec(0.3f);

            recording = false;
            yield return null;
            WriteSoundtrack(Path.Combine(dir, "soundtrack.wav"), T);
            Debug.Log($"[Trailer] wrote {frame} frames ({T:0.0}s) to {Path.GetFullPath(dir)}");
            Application.Quit();
        }

        IEnumerator Capture()
        {
            var eof = new WaitForEndOfFrame();
            while (recording)
            {
                yield return eof;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, $"f{frame:00000}.jpg"), tex.EncodeToJPG(93));
                Destroy(tex);
                frame++;
            }
        }

        void Update()
        {
            fade = Mathf.MoveTowards(fade, fadeTarget, Time.deltaTime / 0.3f);
            cardAlpha = Mathf.MoveTowards(cardAlpha, cardTitle != null ? 1 : 0, Time.deltaTime / 0.4f);
        }

        // ------------------------------------------------------------------ overlay
        void OnGUI()
        {
            GUI.depth = -100;
            if (sLogo == null)
            {
                sLogo = new GUIStyle(GUI.skin.label) { fontSize = 120, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                sCap = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                sSub = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
                sSmall = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            }
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float W = Screen.width / scale, H = 720;

            // cinematic bars
            Box(new Rect(0, 0, W, 54), Color.black);
            Box(new Rect(0, H - 54, W, 54), Color.black);

            if (cardAlpha > 0.001f)
            {
                Box(new Rect(0, 54, W, H - 108), new Color(0.02f, 0.02f, 0.06f, 0.55f * cardAlpha));
                if (cardTitle != null)
                {
                    float bob = Mathf.Sin(Time.time * 2) * 5;
                    Text(new Rect(0, 160 + bob, W, 150), cardTitle, sLogo, new Color(0.75f, 0.95f, 1f, cardAlpha), 4);
                    if (cardSub != null) Text(new Rect(0, 300, W, 40), cardSub, sSub, new Color(1, 1, 1, 0.9f * cardAlpha), 2);
                    if (cardLogo)
                    {
                        float size = 72, gap = 30, total = 5 * size + 4 * gap;
                        for (int i = 0; i < 5; i++)
                        {
                            float y = 370 + Mathf.Sin(Time.time * 3 + i) * 10;
                            var r = new Rect(W * 0.5f - total * 0.5f + i * (size + gap), y, size, size);
                            GUI.DrawTexture(r, Art.FormSprite((Form)i, (int)(Time.time * 4) % 2).texture, ScaleMode.ScaleToFit);
                        }
                    }
                    if (cardSmall != null) Text(new Rect(0, 500, W, 30), cardSmall, sSmall, new Color(1, 0.9f, 0.6f, 0.9f * cardAlpha), 2);
                }
            }

            // captions
            if (T < capEnd && (capTitle != null || capSub != null))
            {
                float a = Mathf.Clamp01((T - capStart) / 0.25f) * Mathf.Clamp01((capEnd - T) / 0.35f);
                if (!string.IsNullOrEmpty(capTitle)) Text(new Rect(0, H - 190, W, 60), capTitle, sCap, new Color(1, 1, 1, a), 3);
                if (!string.IsNullOrEmpty(capSub)) Text(new Rect(0, H - 135, W, 40), capSub, sSub, new Color(1, 0.9f, 0.6f, a), 2);
            }

            if (fade > 0.001f) Box(new Rect(0, 0, W, H), new Color(0, 0, 0, fade));
        }

        static void Box(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Text(Rect r, string s, GUIStyle st, Color c, float shadow)
        {
            st.normal.textColor = new Color(0, 0, 0, c.a * 0.75f);
            GUI.Label(new Rect(r.x + shadow, r.y + shadow, r.width, r.height), s, st);
            st.normal.textColor = c;
            GUI.Label(r, s, st);
        }

        // ------------------------------------------------------------------ audio
        void WriteSoundtrack(string path, float seconds)
        {
            int sr = Sfx.SampleRate;
            int len = Mathf.CeilToInt(seconds * sr);
            var mix = new float[len];

            var music = Sfx.MusicClip;
            if (music != null)
            {
                var m = new float[music.samples];
                music.GetData(m, 0);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)sr;
                    float env = Mathf.Clamp01(t / 1.0f) * Mathf.Clamp01((seconds - t) / 1.5f);
                    mix[i] = m[i % m.Length] * Sfx.MusicVolume * 1.4f * env;
                }
            }

            var cache = new Dictionary<string, float[]>();
            foreach (var (t, name, vol, pitch) in sounds)
            {
                if (!cache.TryGetValue(name, out var data))
                {
                    var c = Sfx.Clip(name);
                    if (c == null) continue;
                    data = new float[c.samples];
                    c.GetData(data, 0);
                    cache[name] = data;
                }
                int start = Mathf.RoundToInt(t * sr);
                for (int j = 0; ; j++)
                {
                    float src = j * pitch;
                    int si = (int)src;
                    if (si >= data.Length - 1 || start + j >= len) break;
                    float frac = src - si;
                    mix[start + j] += Mathf.Lerp(data[si], data[si + 1], frac) * vol;
                }
            }

            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + len * 2);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(sr); w.Write(sr * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(len * 2);
                for (int i = 0; i < len; i++)
                    w.Write((short)(Mathf.Clamp((float)Math.Tanh(mix[i] * 1.1f), -1f, 1f) * 32000));
            }
        }
    }
}
