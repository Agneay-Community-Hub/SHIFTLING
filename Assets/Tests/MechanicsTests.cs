using System;
using System.Collections;
using NUnit.Framework;
using Shiftling;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Drives the real game with simulated input to check that each form's ability
/// and weakness behave as the level design expects.
/// </summary>
public class MechanicsTests
{
    Game G => Game.I;
    Player P => Game.I.player;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Controls.Sim = true;
        Controls.SimReset();
        if (Game.I == null) new GameObject("Shiftling").AddComponent<Game>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        Controls.SimReset();
        Controls.Sim = false;
        Time.timeScale = 1;
    }

    static IEnumerator Wait(float seconds, Func<bool> until = null)
    {
        float t = 0;
        while (t < seconds)
        {
            if (until != null && until()) yield break;
            t += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator Begin(int level, int x, int row, Form f)
    {
        G.StartLevel(level);
        G.UnlockAll();
        yield return null;
        G.PlaceAt(x, row, f);
        yield return Wait(0.6f);
    }

    /// <summary>Hold right and tap jump periodically until done or timeout.</summary>
    IEnumerator RunRight(float seconds, Func<bool> until, float jumpEvery = 0.35f, Func<bool> allowJump = null)
    {
        Controls.SimX = 1;
        float t = 0, jt = 0;
        while (t < seconds && !until())
        {
            t += Time.deltaTime;
            jt += Time.deltaTime;
            bool may = allowJump == null || allowJump();
            Controls.SimJump(may && jt % jumpEvery < jumpEvery * 0.6f);
            yield return null;
        }
        Controls.SimReset();
    }

    IEnumerator MeasureRise(Action<float> result, bool doubleJump)
    {
        float baseY = P.Bounds.yMin, maxY = baseY;
        Controls.SimJump(true);
        bool second = false;
        float t = 0;
        while (t < 1.6f)
        {
            t += Time.deltaTime;
            maxY = Mathf.Max(maxY, P.Bounds.yMin);
            if (doubleJump && !second && t > 0.1f && P.Rb.linearVelocity.y <= 0.5f)
            {
                second = true;
                Controls.SimJump(false);
                yield return null;
                Controls.SimJump(true);
            }
            yield return null;
        }
        Controls.SimReset();
        result(maxY - baseY);
    }

    [UnityTest]
    public IEnumerator WispSingleJumpClearsTwoTilesButNotThree()
    {
        yield return Begin(0, 4, 9, Form.Wisp);
        float rise = 0;
        yield return MeasureRise(r => rise = r, false);
        Debug.Log($"Wisp single jump rise {rise:0.00}");
        Assert.That(rise, Is.InRange(2.05f, 2.95f));
    }

    [UnityTest]
    public IEnumerator WispDoubleJumpClearsThreeTiles()
    {
        yield return Begin(0, 4, 9, Form.Wisp);
        float rise = 0;
        yield return MeasureRise(r => rise = r, true);
        Debug.Log($"Wisp double jump rise {rise:0.00}");
        Assert.That(rise, Is.InRange(3.1f, 4.8f));
    }

    [UnityTest]
    public IEnumerator FrogJumpClearsFourTiles()
    {
        yield return Begin(0, 4, 9, Form.Frog);
        float rise = 0;
        yield return MeasureRise(r => rise = r, false);
        Debug.Log($"Frog jump rise {rise:0.00}");
        Assert.That(rise, Is.GreaterThan(4.05f));
    }

    [UnityTest]
    public IEnumerator BoulderCannotJump()
    {
        yield return Begin(1, 4, 10, Form.Boulder);
        float rise = 0;
        yield return MeasureRise(r => rise = r, false);
        Assert.That(rise, Is.LessThan(0.2f));
    }

    [UnityTest]
    public IEnumerator Level1_FrogSwimsUnderWallToExit()
    {
        yield return Begin(0, 39, 9, Form.Frog);
        // hop up the steps, then sink under the divider, then jump out at the far side
        yield return RunRight(25f, () => G.IsLevelComplete,
            0.4f, () => P.Rb.position.x < 44.5f || P.Rb.position.x > 51f);
        Assert.IsTrue(G.IsLevelComplete, $"frog stuck at {P.Rb.position}");
        Assert.AreEqual(0, G.LevelDeaths);
    }

    [UnityTest]
    public IEnumerator Level1_WispDiesInWater()
    {
        yield return Begin(0, 42, 7, Form.Wisp);
        bool died = false;
        yield return RunRight(5f, () => died |= P.dead, 0.4f);
        Assert.IsTrue(died || G.LevelDeaths > 0);
    }

    [UnityTest]
    public IEnumerator Level2_BoulderCrushesFloorPushesCrateOpensDoor()
    {
        yield return Begin(1, 33, 10, Form.Boulder);
        yield return RunRight(30f, () => P.Rb.position.x > 58f, 10f, () => false);
        Assert.IsTrue(G.level.doorsOpen, $"door should be held open by the crate on the plate (crate {G.level.crates[0].transform.position}, player {P.Rb.position})");
        Assert.Greater(P.Rb.position.x, 57.5f, "boulder should have passed the door");
        Assert.AreEqual(0, G.LevelDeaths);
        Assert.IsTrue(P.TryShift(Form.Wisp), "should have room to become a wisp");
        yield return RunRight(15f, () => G.IsLevelComplete, 0.3f);
        Assert.IsTrue(G.IsLevelComplete, $"wisp stuck at {P.Rb.position}");
    }

    [UnityTest]
    public IEnumerator Level2_WispDiesOnSpikes_BoulderDoesNot()
    {
        yield return Begin(1, 11, 10, Form.Boulder);
        yield return RunRight(6f, () => P.Rb.position.x > 31f, 10f, () => false);
        Assert.AreEqual(0, G.LevelDeaths, "boulder should ignore spikes");
        yield return Begin(1, 12, 10, Form.Wisp);
        bool died = false;
        Controls.SimX = 1;
        yield return Wait(3f, () => died |= P.dead);
        Controls.SimReset();
        Assert.IsTrue(died, "wisp should die on spikes");
    }

    [UnityTest]
    public IEnumerator Level3_BatFliesOverWallAndPool()
    {
        yield return Begin(2, 3, 19, Form.Bat);
        Controls.SimJump(true);
        Controls.SimX = 0.0f;
        yield return Wait(2.2f);       // climb
        Controls.SimX = 1;
        yield return Wait(6f, () => P.Rb.position.x > 25.5f && P.grounded);
        Controls.SimReset();
        yield return Wait(1f);
        Assert.AreEqual(0, G.LevelDeaths, "bat should cross the pool safely");
        Assert.Greater(P.Rb.position.x, 24.5f);
    }

    [UnityTest]
    public IEnumerator Level3_WindLiftsWispToExit()
    {
        yield return Begin(2, 27, 12, Form.Wisp);
        Controls.SimX = 1;
        yield return Wait(12f, () => G.IsLevelComplete);
        Controls.SimReset();
        Assert.IsTrue(G.IsLevelComplete, $"wisp stuck at {P.Rb.position}");
    }

    [UnityTest]
    public IEnumerator Level3_WindFlingsBatIntoSpikes()
    {
        yield return Begin(2, 27, 12, Form.Bat);
        Controls.SimX = 1;
        bool died = false;
        yield return Wait(6f, () => died |= P.dead);
        Controls.SimReset();
        Assert.IsTrue(died, "bat should be thrown into the ceiling spikes");
    }

    [UnityTest]
    public IEnumerator Level4_EmberBurnsBarrierAndSwimsLava()
    {
        yield return Begin(3, 3, 11, Form.Ember);
        yield return RunRight(20f, () => P.Rb.position.x > 36f, 0.3f);
        Assert.AreEqual(0, G.LevelDeaths, "ember should survive lava");
        Assert.Greater(P.Rb.position.x, 35.5f, $"ember stuck at {P.Rb.position}");
    }

    [UnityTest]
    public IEnumerator Level4_FrogSwimsThroughNarrowPassage()
    {
        yield return Begin(3, 39, 11, Form.Frog);
        yield return RunRight(25f, () => P.Rb.position.x > 61f, 0.4f, () => P.Rb.position.x > 51f);
        Assert.AreEqual(0, G.LevelDeaths);
        Assert.Greater(P.Rb.position.x, 60.5f, $"frog stuck at {P.Rb.position}");
    }

    [UnityTest]
    public IEnumerator Level5_FinalShaftToExit()
    {
        yield return Begin(4, 106, 16, Form.Wisp);
        Controls.SimX = 1;
        yield return Wait(12f, () => G.IsLevelComplete);
        Controls.SimReset();
        Assert.IsTrue(G.IsLevelComplete, $"wisp stuck at {P.Rb.position}");
    }

    [UnityTest]
    public IEnumerator Level5_CratePuzzle()
    {
        yield return Begin(4, 30, 16, Form.Boulder);
        yield return RunRight(30f, () => P.Rb.position.x > 55.5f, 10f, () => false);
        Assert.IsTrue(G.level.doorsOpen, $"crate {G.level.crates[0].transform.position}, player {P.Rb.position}");
        Assert.Greater(P.Rb.position.x, 55f);
    }

    [UnityTest]
    public IEnumerator Level1_FrogShrineTeachesFrog()
    {
        G.StartLevel(0);
        yield return null;
        G.PlaceAt(34, 4, Form.Wisp);   // on top of the tall block, then drop down to the shrine
        Controls.SimX = 1;
        string trace = "";
        yield return Wait(3f, () =>
        {
            var c = G.level.Cell(P.Rb.position);
            trace += $"[{P.Rb.position.x:0.0},{P.Rb.position.y:0.0} cell {c} '{G.level.Get(c)}'] ";
            return P.form == Form.Frog;
        });
        Controls.SimReset();
        Assert.IsTrue(G.IsUnlocked(Form.Frog), $"shrine should unlock the frog. cell(37,9)='{G.level.Get(37, 9)}' fromShrine={FormDef.FromShrine(G.level.Get(37, 9))} " + trace.Substring(Mathf.Max(0, trace.Length - 1500)));
        Assert.AreEqual(Form.Frog, P.form, $"player at {P.Rb.position}");
    }

    [UnityTest]
    public IEnumerator BoulderNeedsRoom()
    {
        // inside the frog-only underwater passage of level 4 there is no room for a boulder
        yield return Begin(3, 49, 13, Form.Frog);
        Assert.IsFalse(P.TryShift(Form.Boulder));
        Assert.AreEqual(Form.Frog, P.form);
    }
}
