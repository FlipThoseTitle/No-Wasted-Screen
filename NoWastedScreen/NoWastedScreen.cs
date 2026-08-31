using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System;
using System.Diagnostics;

namespace NoWastedScreen
{
    /// <summary>
    /// A cleaner replacement for the vanilla Story Mode wasted screen.
    /// </summary>
    public sealed class NoWastedScreen : Script
    {
        private enum DeathState
        {
            Normal,
            Dead,
            FadingOut,
            FadingIn
        }

        private const string LogTag = "[NoWastedScreen] ";

        // How long the ped has to stay still before we start fading out.
        private const int StillDelayMs = 1000;

        private const int FadeOutDurationMs = 1200;
        private const int FadeInDurationMs = 1000;

        // Safety net so a weird ragdoll/death animation can't trap the player in the Dead state forever.
        private const int MaximumDeathWaitMs = 10000;

        private DeathState state = DeathState.Normal;

        private readonly Stopwatch deathTimer = new Stopwatch();
        private readonly Stopwatch stillTimer = new Stopwatch();

        private Vector3 deathPosition;
        private float deathHeading;

        private bool fadeOutStarted;
        private bool respawnApplied;

        public NoWastedScreen()
        {
            Interval = 0;

            Tick += OnTick;
            Aborted += OnAborted;

            // Stop the vanilla death system from fading and restarting on its own, everything past this point is handled by hand.
            Function.Call(Hash.SET_FADE_OUT_AFTER_DEATH, false);
            Function.Call(Hash.SET_FADE_IN_AFTER_DEATH_ARREST, false);
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                Ped ped = Game.Player.Character;
                if (ped == null || !ped.Exists())
                    return;

                switch (state)
                {
                    case DeathState.Normal:
                        UpdateNormal(ped);
                        break;
                    case DeathState.Dead:
                        UpdateDead(ped);
                        break;
                    case DeathState.FadingOut:
                        UpdateFadingOut(ped);
                        break;
                    case DeathState.FadingIn:
                        UpdateFadingIn(ped);
                        break;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(LogTag + ex);
                EmergencyRecovery();
            }
        }

        private void UpdateNormal(Ped ped)
        {
            Function.Call(Hash.SET_FADE_OUT_AFTER_DEATH, false);

            if (!ped.IsDead)
                return;

            BeginDeath(ped);
        }

        private void BeginDeath(Ped ped)
        {
            state = DeathState.Dead;

            deathTimer.Restart();
            stillTimer.Reset();

            fadeOutStarted = false;
            respawnApplied = false;

            // Grab the death spot before anything else touches the ped.
            deathPosition = ped.Position;
            deathHeading = ped.Heading;

            // Not invincibility, the ped is left genuinely dead so the ragdoll plays out on its own.
            // Player control isn't touched either, since GTA has already taken it away by this point.
            Game.Player.IsInvincible = false;

            SuppressVanillaRespawnController();
        }

        private void UpdateDead(Ped ped)
        {
            SuppressVanillaRespawnController();
            Function.Call(Hash.SET_FADE_OUT_AFTER_DEATH, false);

            // Let the ragdoll play out and only move on once the ped has actually come to rest.
            // fading out while it's still sliding or twitching looks wrong.
            float speed = ped.Velocity.Length();

            if (speed < 0.05f)
            {
                if (!stillTimer.IsRunning)
                    stillTimer.Restart();
            }
            else
            {
                stillTimer.Reset();
            }

            bool hasSettled = stillTimer.IsRunning && stillTimer.ElapsedMilliseconds >= StillDelayMs;
            bool timedOut = deathTimer.ElapsedMilliseconds >= MaximumDeathWaitMs;

            if (hasSettled || timedOut)
                BeginFadeOut();
        }

        private void BeginFadeOut()
        {
            if (fadeOutStarted)
                return;

            fadeOutStarted = true;
            state = DeathState.FadingOut;

            Screen.FadeOut(FadeOutDurationMs);
        }

        private void UpdateFadingOut(Ped ped)
        {
            SuppressVanillaRespawnController();

            if (!Screen.IsFadedOut)
                return;

            ApplyRespawn(ped);
        }

        private void ApplyRespawn(Ped ped)
        {
            if (respawnApplied)
                return;

            respawnApplied = true;

            SuppressVanillaRespawnController();

            // Story Mode ped resurrection
            // note: NETWORK_RESURRECT_LOCAL_PLAYER is an Online only native and doesnt apply here.
            ped.Resurrect();

            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, ped,
                deathPosition.X, deathPosition.Y, deathPosition.Z + 0.15f,
                false, false, false);
            Function.Call(Hash.SET_ENTITY_HEADING, ped, deathHeading);
            Function.Call(Hash.SET_ENTITY_VELOCITY, ped, 0.0f, 0.0f, 0.0f);
            Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, ped);

            ped.Health = ped.MaxHealth;
            ped.IsVisible = true;

            // Stay invincible while the screen is still black, so nothing can kill the ped again before the player has control back.
            Game.Player.IsInvincible = true;

            Function.Call(Hash.FORCE_GAME_STATE_PLAYING);

            state = DeathState.FadingIn;
        }

        private void UpdateFadingIn(Ped ped)
        {
            // Suppression stays on through this whole state.
            // Lifting it early would let respawn_controller notice the old death and queue a delayed hospital restart on top of what we just did.
            SuppressVanillaRespawnController();

            if (ped.IsDead)
                return;

            Game.Player.IsInvincible = true;
            Function.Call(Hash.SET_TIME_SCALE, 1.0f);

            if (Screen.IsFadedOut)
                Screen.FadeIn(FadeInDurationMs);

            if (Screen.IsFadingIn)
                return;

            // Only hand control back to the vanilla restart system once the whole visual transition has actually finished.
            Function.Call(Hash.IGNORE_NEXT_RESTART, false);
            Function.Call(Hash.FORCE_GAME_STATE_PLAYING);
            Function.Call(Hash.SET_TIME_SCALE, 1.0f);

            Game.Player.IsInvincible = false;

            FinishDeathCycle();
        }

        private void FinishDeathCycle()
        {
            state = DeathState.Normal;

            deathTimer.Reset();
            stillTimer.Reset();

            fadeOutStarted = false;
            respawnApplied = false;

            deathPosition = Vector3.Zero;
            deathHeading = 0.0f;

            // Release the pause now that our sequence is done
            // otherwise a later natural death/arrest would stay stuck for the rest of the session.
            Function.Call(Hash.PAUSE_DEATH_ARREST_RESTART, false);
            Function.Call(Hash.IGNORE_NEXT_RESTART, false);
            Function.Call(Hash.FORCE_GAME_STATE_PLAYING);
            Function.Call(Hash.SET_TIME_SCALE, 1.0f);
            Function.Call(Hash.SET_FADE_OUT_AFTER_DEATH, false);
            Function.Call(Hash.SET_FADE_IN_AFTER_DEATH_ARREST, false);

            Game.Player.IsInvincible = false;
        }

        private void EmergencyRecovery()
        {
            try
            {
                Ped ped = Game.Player.Character;

                if (ped != null && ped.Exists())
                {
                    if (ped.IsDead)
                    {
                        ped.Resurrect();

                        Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, ped,
                            deathPosition.X, deathPosition.Y, deathPosition.Z + 0.15f,
                            false, false, false);
                        Function.Call(Hash.SET_ENTITY_HEADING, ped, deathHeading);

                        ped.Health = ped.MaxHealth;
                        ped.IsVisible = true;
                    }

                    Game.Player.IsInvincible = false;
                }

                Function.Call(Hash.PAUSE_DEATH_ARREST_RESTART, false);
                Function.Call(Hash.SET_FADE_OUT_AFTER_DEATH, false);

                if (Screen.IsFadedOut || Screen.IsFadingOut)
                    Screen.FadeIn(300);

                state = DeathState.Normal;

                deathTimer.Reset();
                stillTimer.Reset();

                fadeOutStarted = false;
                respawnApplied = false;
            }
            catch (Exception recoveryException)
            {
                Trace.WriteLine(LogTag + "Emergency recovery failed: " + recoveryException);
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                // Hand everything back to the game as if this script had never been loaded.
                Function.Call(Hash.PAUSE_DEATH_ARREST_RESTART, false);
                Function.Call(Hash.IGNORE_NEXT_RESTART, false);
                Function.Call(Hash.FORCE_GAME_STATE_PLAYING);
                Function.Call(Hash.SET_TIME_SCALE, 1.0f);
                Function.Call(Hash.SET_FADE_OUT_AFTER_DEATH, true);
                Function.Call(Hash.SET_FADE_IN_AFTER_DEATH_ARREST, true);

                Game.Player.IsInvincible = false;

                Ped ped = Game.Player.Character;
                if (ped != null && ped.Exists())
                    ped.IsVisible = true;

                if (Screen.IsFadedOut || Screen.IsFadingOut)
                    Screen.FadeIn(300);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(LogTag + "Abort cleanup failed: " + ex);
            }
        }

        // Keeps the vanilla respawn_controller thread from taking the death over for as long as our own sequence is running.
        // This is called every tick rather than once, because killing the thread a single time isn't enough to stop the game's scheduler from relaunching it mid-sequence.
        private static void SuppressVanillaRespawnController()
        {
            Function.Call(Hash.FORCE_CLEANUP_FOR_ALL_THREADS_WITH_THIS_NAME, "respawn_controller", 3);
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "respawn_controller");

            // Global 5 is something respawn_controller reads on startup.
            // Writing 4 here appears to make it bail out instead of rerunning its entry logic, which is what was requeuing the death/wasted audio cue every time it got relaunched.
            // This is not really documented, it's true global state shared with anything else that might touch the same index
            // if radio, HUD, or another mod starts acting up, this line is the first thing to suspect.
            GlobalVariable.Get(5).Write(4);

            Function.Call(Hash.IGNORE_NEXT_RESTART, true);
            Function.Call(Hash.FORCE_GAME_STATE_PLAYING);
            Function.Call(Hash.SET_TIME_SCALE, 1.0f);
            Function.Call(Hash.SET_FADE_OUT_AFTER_DEATH, false);
            Function.Call(Hash.SET_FADE_IN_AFTER_DEATH_ARREST, false);
        }
    }
}