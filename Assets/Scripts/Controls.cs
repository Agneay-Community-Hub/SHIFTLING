using UnityEngine;
using UnityEngine.InputSystem;

namespace Shiftling
{
    /// <summary>Thin wrapper over the Input System for keyboard + gamepad.</summary>
    public static class Controls
    {
        static Keyboard K => Keyboard.current;
        static Gamepad G => Gamepad.current;

        // ---- simulated input (used by automated PlayMode tests)
        public static bool Sim;
        public static float SimX;
        static bool simJump, simJumpPending;
        public static void SimJump(bool held)
        {
            if (held && !simJump) simJumpPending = true;
            simJump = held;
        }
        public static void SimReset() { SimX = 0; simJump = false; simJumpPending = false; }

        static bool Held(params Key[] keys)
        {
            if (K == null) return false;
            foreach (var k in keys) if (K[k].isPressed) return true;
            return false;
        }

        static bool Down(params Key[] keys)
        {
            if (K == null) return false;
            foreach (var k in keys) if (K[k].wasPressedThisFrame) return true;
            return false;
        }

        public static float X
        {
            get
            {
                if (Sim) return SimX;
                float x = 0;
                if (Held(Key.A, Key.LeftArrow)) x -= 1;
                if (Held(Key.D, Key.RightArrow)) x += 1;
                if (G != null)
                {
                    float s = G.leftStick.x.ReadValue() + G.dpad.x.ReadValue();
                    if (Mathf.Abs(s) > 0.3f) x = Mathf.Sign(s);
                }
                return Mathf.Clamp(x, -1, 1);
            }
        }

        public static bool DownHeld =>
            Held(Key.S, Key.DownArrow) || (G != null && (G.leftStick.y.ReadValue() < -0.5f || G.dpad.down.isPressed));

        public static bool JumpHeld => Sim ? simJump :
            Held(Key.Space, Key.W, Key.UpArrow, Key.Z) || (G != null && G.buttonSouth.isPressed);

        public static bool JumpDown
        {
            get
            {
                if (Sim) { bool p = simJumpPending; simJumpPending = false; return p; }
                return Down(Key.Space, Key.W, Key.UpArrow, Key.Z) || (G != null && G.buttonSouth.wasPressedThisFrame);
            }
        }

        public static bool Confirm =>
            Down(Key.Enter, Key.NumpadEnter, Key.Space) || (G != null && (G.buttonSouth.wasPressedThisFrame || G.startButton.wasPressedThisFrame));

        public static bool Pause => Down(Key.Escape, Key.P) || (G != null && G.startButton.wasPressedThisFrame);
        public static bool Restart => Down(Key.R) || (G != null && G.selectButton.wasPressedThisFrame);
        public static bool Mute => Down(Key.M);
        public static bool CycleNext => Down(Key.E) || (G != null && G.rightShoulder.wasPressedThisFrame);
        public static bool CyclePrev => Down(Key.Q) || (G != null && G.leftShoulder.wasPressedThisFrame);
        public static bool SwapLast => Down(Key.LeftShift, Key.RightShift, Key.Tab) || (G != null && G.buttonWest.wasPressedThisFrame);

        /// <summary>Returns 0..4 when a number key is pressed this frame, else -1.</summary>
        public static int FormKey
        {
            get
            {
                if (K == null) return -1;
                Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
                Key[] pad = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5 };
                for (int i = 0; i < keys.Length; i++)
                    if (K[keys[i]].wasPressedThisFrame || K[pad[i]].wasPressedThisFrame) return i;
                return -1;
            }
        }
    }
}
