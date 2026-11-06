using UnityEngine;

namespace Shiftling
{
    public class Game : MonoBehaviour
    {
        enum State { Title, Play, Paused, Complete, End }

        public static Game I;
        public Level level;
        public Player player;
        public bool Playing => state == State.Play;

        State state = State.Title;
        int levelIndex;
        int titleSelect;
        int maxReached;
        readonly bool[] unlocked = new bool[5];
        Vector2 respawnPos;
        Form respawnForm;
        Vector2Int lastCheckpoint = new Vector2Int(-1, -1);
        float respawnTimer = -1;
        int deaths, levelDeaths;
        float runTime, levelTime;
        string toast; float toastT;
        string bigTitle, bigSub1, bigSub2; float bigT; Color bigCol;
        float shake;
        float hintT;

        Camera cam;
        Vector3 camVel;
        Transform bgRoot;
        SpriteRenderer sky;
        (SpriteRenderer[] srs, float factor, float yOff, float w)[] layers;
        int bgPal = -1;

        // ------------------------------------------------------------------ boot
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Shiftling");
            go.AddComponent<Game>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-trailer") >= 0)
                go.AddComponent<Trailer>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Physics2D.gravity = new Vector2(0, -30f);
            Time.fixedDeltaTime = 1f / 60f;

            cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera");
                cam = cgo.AddComponent<Camera>();
                cgo.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.backgroundColor = Color.black;
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            Fx.Init(transform);
            Sfx.Init(gameObject);
            maxReached = Mathf.Clamp(PlayerPrefs.GetInt("shiftling_level", 0), 0, LevelData.Maps.Length - 1);
            titleSelect = maxReached;
            BuildBackground();
            LoadLevel(titleSelect, true);
        }

        // ------------------------------------------------------------------ level flow
        void LoadLevel(int index, bool titleMode = false)
        {
            levelIndex = index;
            Fx.Clear();
            if (level != null) Destroy(level.gameObject);
            if (player != null) Destroy(player.gameObject);
            level = Level.Build(LevelData.Maps[index], index % Art.Palettes.Length, transform);
            SetBackgroundPalette(level.pal);

            for (int i = 0; i < unlocked.Length; i++) unlocked[i] = i == 0;
            for (int li = 0; li < index; li++)
                foreach (var row in LevelData.Maps[li])
                    foreach (var ch in row)
                    {
                        var f = FormDef.FromShrine(ch);
                        if (f.HasValue) unlocked[(int)f.Value] = true;
                    }

            lastCheckpoint = new Vector2Int(-1, -1);
            respawnForm = Form.Wisp;
            respawnPos = BottomAligned(level.spawn, Form.Wisp);
            levelDeaths = 0;
            levelTime = 0;
            hintT = 0;
            respawnTimer = -1;

            if (!titleMode)
            {
                player = Player.Create(transform);
                player.Respawn(respawnPos, Form.Wisp);
                state = State.Play;
                cam.transform.position = new Vector3(respawnPos.x, respawnPos.y, -10);
                ClampCamera();
                Big(LevelData.Names[index], $"Level {index + 1} of {LevelData.Maps.Length}", "", Art.Palettes[level.pal].accent, 2.5f);
            }
            else
            {
                state = State.Title;
                cam.transform.position = new Vector3(level.W * 0.5f, level.H * 0.5f, -10);
            }
            Time.timeScale = 1;
        }

        Vector2 BottomAligned(Vector2 cellCenter, Form f)
        {
            var s = FormDef.Get(f).size;
            return new Vector2(cellCenter.x, cellCenter.y - 0.5f + s.y * 0.5f + 0.02f);
        }

        public bool IsUnlocked(Form f) => unlocked[(int)f];

        // ---- hooks for automated tests
        public void StartLevel(int index) => LoadLevel(index);
        public void ShowTitle(int index) => LoadLevel(index, true);
        public bool HideHud;
        public void SnapCamera()
        {
            if (player == null) return;
            var p = player.Rb.position;
            cam.transform.position = new Vector3(p.x, p.y + 1f, -10);
            camVel = Vector3.zero;
            ClampCamera();
        }
        public bool IsLevelComplete => state == State.Complete;
        public int LevelDeaths => levelDeaths;
        public void UnlockAll() { for (int i = 0; i < unlocked.Length; i++) unlocked[i] = true; }
        public void PlaceAt(int x, int row, Form f)
        {
            respawnTimer = -1;
            player.Respawn(BottomAligned(level.World(x, row), f), f);
        }

        public void TouchShrine(Form f)
        {
            if (unlocked[(int)f]) return;
            unlocked[(int)f] = true;
            var d = FormDef.Get(f);
            Sfx.Play("unlock", 0.9f, 0);
            Big($"New shape: {d.name.ToUpper()}", "+ " + d.ability, "- " + d.weakness, d.color, 5f);
            Toast($"Press {(int)f + 1}, or Q / E, to shift. SHIFT swaps back.");
            toastT = 6f;
            player.TryShift(f);
        }

        public void ReachCheckpoint(Vector2Int c)
        {
            if (c == lastCheckpoint) return;
            lastCheckpoint = c;
            respawnForm = player.form;
            respawnPos = BottomAligned(level.World(c.x, c.y), player.form);
            level.ActivateCheckpoint(c);
            Sfx.Play("checkpoint", 0.8f, 0);
            Fx.Burst(level.World(c.x, c.y) + Vector2.up * 0.3f, new Color(0.5f, 1f, 0.85f), 16, 4f, 0.6f);
            Toast("Checkpoint");
        }

        public void OnPlayerDied(string reason)
        {
            deaths++;
            levelDeaths++;
            Toast(reason);
            toastT = 2.2f;
            respawnTimer = 0.9f;
        }

        public void LevelComplete()
        {
            if (state != State.Play) return;
            state = State.Complete;
            Sfx.Play("win", 1f, 0);
            Fx.Burst(player.Rb.position, Color.white, 40, 8f, 1f);
            player.Rb.linearVelocity = Vector2.zero;
            player.Rb.simulated = false;
            int nextIdx = Mathf.Min(levelIndex + 1, LevelData.Maps.Length - 1);
            maxReached = Mathf.Max(maxReached, nextIdx);
            PlayerPrefs.SetInt("shiftling_level", maxReached);
            PlayerPrefs.Save();
        }

        public void Toast(string msg) { toast = msg; toastT = 2.5f; }

        // Silence everything while the game window / browser tab is not focused.
        void OnApplicationFocus(bool focused) => AudioListener.pause = !focused;
        void OnApplicationPause(bool paused) => AudioListener.pause = paused;

        void Big(string title, string s1, string s2, Color c, float t)
        {
            bigTitle = title; bigSub1 = s1; bigSub2 = s2; bigCol = c; bigT = t;
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            toastT -= dt;
            bigT -= dt;
            if (Controls.Mute) Sfx.ToggleMute();

            switch (state)
            {
                case State.Title:
                    if (Keyboard(UnityEngine.InputSystem.Key.LeftArrow) || Keyboard(UnityEngine.InputSystem.Key.A)) { titleSelect = Mathf.Max(0, titleSelect - 1); Sfx.Play("select"); LoadLevel(titleSelect, true); }
                    if (Keyboard(UnityEngine.InputSystem.Key.RightArrow) || Keyboard(UnityEngine.InputSystem.Key.D)) { titleSelect = Mathf.Min(maxReached, titleSelect + 1); Sfx.Play("select"); LoadLevel(titleSelect, true); }
                    if (Controls.Confirm) { runTime = 0; deaths = 0; LoadLevel(titleSelect); }
                    break;
                case State.Play:
                    runTime += Time.deltaTime;
                    levelTime += Time.deltaTime;
                    hintT += Time.deltaTime;
                    if (Controls.Pause) { state = State.Paused; Time.timeScale = 0; }
                    if (Controls.Restart) { LoadLevel(levelIndex); return; }
                    if (respawnTimer > 0)
                    {
                        respawnTimer -= Time.deltaTime;
                        if (respawnTimer <= 0) player.Respawn(respawnPos, respawnForm);
                    }
                    break;
                case State.Paused:
                    if (Controls.Pause || Controls.Confirm) { state = State.Play; Time.timeScale = 1; }
                    if (Controls.Restart) { Time.timeScale = 1; LoadLevel(levelIndex); }
                    else if (Keyboard(UnityEngine.InputSystem.Key.Q)) { Time.timeScale = 1; titleSelect = levelIndex; LoadLevel(levelIndex, true); }
                    break;
                case State.Complete:
                    if (Controls.Confirm)
                    {
                        if (levelIndex + 1 < LevelData.Maps.Length) LoadLevel(levelIndex + 1);
                        else { state = State.End; Sfx.Play("unlock", 1, 0); }
                    }
                    break;
                case State.End:
                    if (Controls.Confirm) { titleSelect = 0; LoadLevel(0, true); }
                    break;
            }
        }

        static bool Keyboard(UnityEngine.InputSystem.Key k)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb[k].wasPressedThisFrame;
        }

        void LateUpdate()
        {
            if (state == State.Title || state == State.End)
            {
                float t = Time.time * 0.08f;
                var target = new Vector3(Mathf.Lerp(0, level.W, Mathf.PingPong(t, 1)), level.H * 0.5f, -10);
                cam.transform.position = Vector3.Lerp(cam.transform.position, target, Time.deltaTime * 2);
            }
            else if (player != null)
            {
                var p = player.Rb.position;
                var target = new Vector3(p.x, p.y + 1f, -10);
                cam.transform.position = Vector3.SmoothDamp(cam.transform.position, target, ref camVel, 0.18f, Mathf.Infinity, Time.unscaledDeltaTime);
            }
            ClampCamera();
            shake = Mathf.MoveTowards(shake, 0, Time.unscaledDeltaTime * 1.2f);
            if (shake > 0 && state != State.Paused)
                cam.transform.position += (Vector3)(Random.insideUnitCircle * shake * 0.5f);
            UpdateBackground();
        }

        void ClampCamera()
        {
            if (level == null) return;
            float hh = cam.orthographicSize, hw = hh * cam.aspect;
            var p = cam.transform.position;
            float minX = -0.5f + hw, maxX = level.W - 0.5f - hw;
            float minY = -0.5f + hh, maxY = level.H - 0.5f - hh;
            p.x = minX > maxX ? (level.W - 1) * 0.5f : Mathf.Clamp(p.x, minX, maxX);
            p.y = minY > maxY ? (level.H - 1) * 0.5f : Mathf.Clamp(p.y, minY, maxY);
            cam.transform.position = p;
        }

        // ------------------------------------------------------------------ background
        void BuildBackground()
        {
            bgRoot = new GameObject("Background").transform;
            bgRoot.SetParent(transform);
            sky = NewBg("sky", -300);
            layers = new (SpriteRenderer[], float, float, float)[3];
            layers[0] = (new[] { NewBg("stars", -250), NewBg("stars", -250), NewBg("stars", -250) }, 0.05f, 3f, 16f);
            layers[1] = (new[] { NewBg("far", -200), NewBg("far", -200), NewBg("far", -200) }, 0.2f, -1.5f, 16f);
            layers[2] = (new[] { NewBg("near", -150), NewBg("near", -150), NewBg("near", -150) }, 0.45f, -3.5f, 16f);
        }

        SpriteRenderer NewBg(string n, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(bgRoot);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        void SetBackgroundPalette(int pal)
        {
            if (pal == bgPal) return;
            bgPal = pal;
            sky.sprite = Art.Sky(pal);
            foreach (var s in layers[0].srs) s.sprite = Art.Stars(pal);
            foreach (var s in layers[1].srs) s.sprite = Art.Hills(pal, false);
            foreach (var s in layers[2].srs) s.sprite = Art.Hills(pal, true);
        }

        void UpdateBackground()
        {
            if (sky == null) return;
            var c = cam.transform.position;
            float hh = cam.orthographicSize, hw = hh * cam.aspect;
            sky.transform.position = new Vector3(c.x, c.y, 0);
            sky.transform.localScale = new Vector3(hw * 2 + 2, (hh * 2 + 2) / 32f, 1);
            foreach (var (srs, factor, yOff, w) in layers)
            {
                float lx = c.x * factor;
                float baseX = lx + Mathf.Round((c.x - lx) / w) * w;
                float y = c.y - hh + yOff + (factor < 0.1f ? hh : 0);
                for (int i = 0; i < srs.Length; i++)
                    srs[i].transform.position = new Vector3(baseX + (i - 1) * w, y, 0);
            }
        }

        // ------------------------------------------------------------------ GUI
        GUIStyle sTitle, sBig, sMid, sSmall, sTiny, sCenterSmall;
        Texture2D white;

        void Styles()
        {
            if (sTitle != null) return;
            white = Texture2D.whiteTexture;
            sTitle = new GUIStyle(GUI.skin.label) { fontSize = 96, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sBig = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sMid = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sSmall = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperLeft };
            sTiny = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            sCenterSmall = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            foreach (var s in new[] { sTitle, sBig, sMid, sSmall, sTiny, sCenterSmall }) s.normal.textColor = Color.white;
        }

        void Label(Rect r, string text, GUIStyle st, Color col, float shadow = 2)
        {
            var old = st.normal.textColor;
            st.normal.textColor = new Color(0, 0, 0, col.a * 0.7f);
            GUI.Label(new Rect(r.x + shadow, r.y + shadow, r.width, r.height), text, st);
            st.normal.textColor = col;
            GUI.Label(r, text, st);
            st.normal.textColor = old;
        }

        void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void OnGUI()
        {
            Styles();
            if (HideHud) return;
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float W = Screen.width / scale, H = 720;

            switch (state)
            {
                case State.Title: DrawTitle(W, H); break;
                case State.End: DrawEnd(W, H); break;
                default:
                    DrawHud(W, H);
                    if (state == State.Paused) DrawPause(W, H);
                    if (state == State.Complete) DrawComplete(W, H);
                    break;
            }
        }

        void DrawTitle(float W, float H)
        {
            Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.45f));
            float bob = Mathf.Sin(Time.time * 2) * 6;
            Label(new Rect(0, 120 + bob, W, 120), "SHIFTLING", sTitle, new Color(0.75f, 0.95f, 1f));
            Label(new Rect(0, 225, W, 40), "A shapeshifting adventure", sMid, new Color(1, 1, 1, 0.85f));

            // parade of forms
            float size = 64, gap = 26;
            float total = 5 * size + 4 * gap;
            for (int i = 0; i < 5; i++)
            {
                var f = (Form)i;
                float y = 300 + Mathf.Sin(Time.time * 3 + i) * 8;
                var r = new Rect(W * 0.5f - total * 0.5f + i * (size + gap), y, size, size);
                GUI.DrawTexture(r, Art.FormSprite(f, (int)(Time.time * 4) % 2).texture, ScaleMode.ScaleToFit);
                Label(new Rect(r.x - 20, r.yMax + 2, size + 40, 24), FormDef.Get(f).name, sTiny, FormDef.Get(f).color, 1);
            }

            string lvl = maxReached > 0 ? $"<  Level {titleSelect + 1}: {LevelData.Names[titleSelect]}  >" : $"Level 1: {LevelData.Names[0]}";
            Label(new Rect(0, 440, W, 40), lvl, sMid, Color.white);
            float blink = 0.6f + Mathf.Sin(Time.time * 4) * 0.4f;
            Label(new Rect(0, 495, W, 40), "Press ENTER or SPACE to begin", sMid, new Color(1, 0.9f, 0.5f, blink));
            Label(new Rect(0, 590, W, 30), "Move A/D or Arrows  -  Jump SPACE/W  -  Shift shapes 1-5 or Q/E  -  SHIFT swaps back  -  R restart  -  M mute", sTiny, new Color(1, 1, 1, 0.75f), 1);
            Label(new Rect(0, 640, W, 30), "Made for GitHub Game Off 2026  -  theme: SHIFT", sTiny, new Color(1, 1, 1, 0.5f), 1);
        }

        void DrawHud(float W, float H)
        {
            var pal = Art.Palettes[level.pal];
            // top-left info
            Box(new Rect(12, 12, 330, 74), new Color(0, 0, 0, 0.35f));
            Label(new Rect(24, 16, 320, 30), $"{levelIndex + 1}/{LevelData.Maps.Length}  {LevelData.Names[levelIndex]}", sSmall, pal.accent, 1);
            Label(new Rect(24, 46, 320, 30), $"Time {FormatTime(levelTime)}     Deaths {levelDeaths}", sSmall, new Color(1, 1, 1, 0.85f), 1);

            // hint
            float ha = hintT < 14 ? 1f : 0.45f;
            Label(new Rect(W * 0.5f - 420, 18, 840, 60), LevelData.Hints[levelIndex], sCenterSmall, new Color(1, 1, 1, 0.9f * ha), 1);

            // form bar
            float size = 58, gap = 10;
            float total = 5 * size + 4 * gap;
            float x0 = W * 0.5f - total * 0.5f, y0 = H - size - 70;
            Box(new Rect(x0 - 14, y0 - 12, total + 28, size + 76), new Color(0, 0, 0, 0.35f));
            for (int i = 0; i < 5; i++)
            {
                var f = (Form)i;
                var d = FormDef.Get(f);
                var r = new Rect(x0 + i * (size + gap), y0, size, size);
                bool cur = player != null && player.form == f;
                Box(r, cur ? new Color(d.color.r, d.color.g, d.color.b, 0.85f) : new Color(1, 1, 1, 0.12f));
                Box(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), new Color(0.05f, 0.05f, 0.1f, 0.85f));
                if (unlocked[i])
                    GUI.DrawTexture(new Rect(r.x + 7, r.y + 7, r.width - 14, r.height - 14), Art.FormSprite(f).texture, ScaleMode.ScaleToFit);
                else
                    Label(r, "?", sMid, new Color(1, 1, 1, 0.3f), 1);
                Label(new Rect(r.x + 2, r.y, 20, 20), (i + 1).ToString(), sTiny, new Color(1, 1, 1, 0.8f), 1);
            }
            if (player != null)
            {
                var d = FormDef.Get(player.form);
                Label(new Rect(x0 - 200, y0 + size + 4, total + 400, 26), $"{d.name}:  <color=#9dff9d>{d.ability}</color>   <color=#ff9d9d>{d.weakness}</color>", RichTiny(), Color.white, 1);

                float meter = -1; Color mc = Color.white; string mlabel = "";
                if (player.form == Form.Frog) { meter = player.moisture; mc = new Color(0.4f, 0.8f, 1f); mlabel = "moisture"; }
                if (player.form == Form.Bat) { meter = player.stamina; mc = new Color(0.8f, 0.6f, 1f); mlabel = "stamina"; }
                if (meter >= 0)
                {
                    var mr = new Rect(W * 0.5f - 120, y0 - 34, 240, 12);
                    Box(mr, new Color(0, 0, 0, 0.6f));
                    Box(new Rect(mr.x + 2, mr.y + 2, (mr.width - 4) * Mathf.Clamp01(meter), mr.height - 4), mc);
                    Label(new Rect(mr.x, mr.y - 18, mr.width, 18), mlabel, sTiny, new Color(1, 1, 1, 0.8f), 1);
                }
            }

            // big announcement
            if (bigT > 0 && bigTitle != null)
            {
                float a = Mathf.Clamp01(bigT * 2);
                Label(new Rect(0, H * 0.24f, W, 60), bigTitle, sBig, new Color(bigCol.r, bigCol.g, bigCol.b, a));
                if (!string.IsNullOrEmpty(bigSub1)) Label(new Rect(0, H * 0.24f + 58, W, 32), bigSub1, sMid, new Color(0.65f, 1f, 0.65f, a));
                if (!string.IsNullOrEmpty(bigSub2)) Label(new Rect(0, H * 0.24f + 90, W, 32), bigSub2, sMid, new Color(1f, 0.65f, 0.65f, a));
            }
            if (toastT > 0 && toast != null)
            {
                float a = Mathf.Clamp01(toastT * 2);
                Label(new Rect(0, H * 0.24f + 135, W, 36), toast, sMid, new Color(1, 1, 0.85f, a));
            }
        }

        GUIStyle richTiny;
        GUIStyle RichTiny()
        {
            if (richTiny == null) richTiny = new GUIStyle(sTiny) { richText = true, fontSize = 16 };
            return richTiny;
        }

        void DrawPause(float W, float H)
        {
            Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
            Label(new Rect(0, 200, W, 60), "PAUSED", sBig, Color.white);
            Label(new Rect(0, 290, W, 30), "ESC / ENTER resume   -   R restart level   -   Q quit to title   -   M mute", sMid, new Color(1, 1, 1, 0.8f));
            Label(new Rect(0, 360, W, 30), "Every form has an ability and a weakness. Shift often!", sCenterSmall, new Color(1, 1, 1, 0.7f));
        }

        void DrawComplete(float W, float H)
        {
            Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.5f));
            Label(new Rect(0, 210, W, 60), "Level complete!", sBig, Art.Palettes[level.pal].accent);
            Label(new Rect(0, 290, W, 30), $"Time {FormatTime(levelTime)}    Deaths {levelDeaths}", sMid, Color.white);
            string next = levelIndex + 1 < LevelData.Maps.Length ? "Press ENTER for the next level" : "Press ENTER";
            Label(new Rect(0, 360, W, 30), next, sMid, new Color(1, 0.9f, 0.5f, 0.6f + Mathf.Sin(Time.unscaledTime * 4) * 0.4f));
        }

        void DrawEnd(float W, float H)
        {
            Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
            Label(new Rect(0, 160, W, 80), "You mastered every shape", sBig, new Color(1f, 0.85f, 0.5f));
            Label(new Rect(0, 250, W, 30), $"Total time {FormatTime(runTime)}     Total deaths {deaths}", sMid, Color.white);
            float size = 64, gap = 26, total = 5 * size + 4 * gap;
            for (int i = 0; i < 5; i++)
            {
                float y = 320 + Mathf.Abs(Mathf.Sin(Time.time * 4 + i)) * -20;
                GUI.DrawTexture(new Rect(W * 0.5f - total * 0.5f + i * (size + gap), y, size, size), Art.FormSprite((Form)i).texture, ScaleMode.ScaleToFit);
            }
            Label(new Rect(0, 440, W, 30), "Thanks for playing SHIFTLING!", sMid, new Color(0.75f, 0.95f, 1f));
            Label(new Rect(0, 500, W, 30), "Press ENTER to return to the title", sMid, new Color(1, 1, 1, 0.6f + Mathf.Sin(Time.time * 4) * 0.4f));
        }

        static string FormatTime(float t) => $"{(int)(t / 60)}:{(t % 60):00.0}";
    }
}
