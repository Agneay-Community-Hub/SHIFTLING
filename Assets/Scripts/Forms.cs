using UnityEngine;

namespace Shiftling
{
    public enum Form { Wisp = 0, Frog = 1, Boulder = 2, Bat = 3, Ember = 4 }

    public class FormDef
    {
        public Form form;
        public string name;
        public string ability;
        public string weakness;
        public Vector2 size;
        public float speed;
        public float jumpVel;
        public float gravity;
        public float mass;
        public Color color;
        public char shrine;

        public static readonly FormDef[] All =
        {
            new FormDef {
                form = Form.Wisp, name = "Wisp", shrine = '\0',
                ability = "Double jump",
                weakness = "Fizzles in water, spikes and lava",
                size = new Vector2(0.78f, 0.78f), speed = 7f, jumpVel = 12f, gravity = 1f, mass = 1f,
                color = new Color(0.45f, 0.95f, 1f)
            },
            new FormDef {
                form = Form.Frog, name = "Frog", shrine = 'f',
                ability = "Huge jump, swims in water",
                weakness = "Dries out on land",
                size = new Vector2(0.82f, 0.72f), speed = 6f, jumpVel = 16f, gravity = 1f, mass = 1f,
                color = new Color(0.45f, 0.9f, 0.35f)
            },
            new FormDef {
                form = Form.Boulder, name = "Boulder", shrine = 'b',
                ability = "Ignores spikes, cracks floors, pushes crates, holds plates",
                weakness = "Can't jump, sinks in water, big & slow",
                size = new Vector2(1.8f, 1.8f), speed = 3.6f, jumpVel = 0f, gravity = 1.6f, mass = 20f,
                color = new Color(0.65f, 0.6f, 0.55f)
            },
            new FormDef {
                form = Form.Bat, name = "Bat", shrine = 'v',
                ability = "Flies while you hold JUMP",
                weakness = "Tires quickly, flung by wind, hates water",
                size = new Vector2(0.75f, 0.55f), speed = 5.2f, jumpVel = 7f, gravity = 0.6f, mass = 0.5f,
                color = new Color(0.7f, 0.5f, 1f)
            },
            new FormDef {
                form = Form.Ember, name = "Ember", shrine = 'e',
                ability = "Swims in lava, burns wood, fast",
                weakness = "Snuffed out by water",
                size = new Vector2(0.7f, 0.8f), speed = 8.5f, jumpVel = 13f, gravity = 1f, mass = 1f,
                color = new Color(1f, 0.6f, 0.2f)
            },
        };

        public static FormDef Get(Form f) => All[(int)f];

        public static Form? FromShrine(char c)
        {
            foreach (var d in All) if (d.shrine == c && c != '\0') return d.form;
            return null;
        }
    }
}
