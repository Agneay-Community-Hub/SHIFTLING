using System.Collections.Generic;
using UnityEngine;

namespace Shiftling
{
    /// <summary>Lightweight pooled sprite particles.</summary>
    public class Fx : MonoBehaviour
    {
        class P
        {
            public SpriteRenderer sr;
            public Vector2 v;
            public float life, max, grav, size, drag;
            public Color col;
        }

        static Fx inst;
        readonly List<P> live = new List<P>();
        readonly Stack<SpriteRenderer> free = new Stack<SpriteRenderer>();

        public static void Init(Transform parent)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(parent);
            inst = go.AddComponent<Fx>();
        }

        public static void Burst(Vector2 pos, Color col, int count, float speed, float life = 0.6f, float grav = 0f, float size = 0.15f, float spread = 0.2f)
        {
            if (inst == null) return;
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.3f, 1f);
                inst.Spawn(pos + Random.insideUnitCircle * spread, v, col, life * Random.Range(0.6f, 1.2f), grav, size);
            }
        }

        public static void Emit(Vector2 pos, Vector2 vel, Color col, float life, float grav, float size)
        {
            if (inst != null) inst.Spawn(pos, vel, col, life, grav, size);
        }

        public static void Clear()
        {
            if (inst == null) return;
            foreach (var p in inst.live) { p.sr.gameObject.SetActive(false); inst.free.Push(p.sr); }
            inst.live.Clear();
        }

        void Spawn(Vector2 pos, Vector2 v, Color col, float life, float grav, float size)
        {
            if (live.Count > 600) return;
            SpriteRenderer sr;
            if (free.Count > 0) { sr = free.Pop(); sr.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("p");
                go.transform.SetParent(transform);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Art.Dot();
                sr.sortingOrder = 30;
            }
            sr.transform.position = pos;
            sr.color = col;
            live.Add(new P { sr = sr, v = v, life = life, max = life, grav = grav, size = size, col = col, drag = 2f });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.life -= dt;
                if (p.life <= 0)
                {
                    p.sr.gameObject.SetActive(false);
                    free.Push(p.sr);
                    live.RemoveAt(i);
                    continue;
                }
                p.v.y -= p.grav * dt;
                p.v *= 1f / (1f + p.drag * dt);
                p.sr.transform.position += (Vector3)(p.v * dt);
                float k = p.life / p.max;
                float s = p.size * 8f * Mathf.Lerp(0.3f, 1f, k);   // dot sprite is 1/8 unit
                p.sr.transform.localScale = new Vector3(s, s, 1);
                var c = p.col; c.a *= Mathf.Clamp01(k * 1.5f);
                p.sr.color = c;
            }
        }
    }
}
