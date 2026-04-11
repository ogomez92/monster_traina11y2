using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce moon phase changes (Luna clan mechanic).
    /// Hooks PlayerManager.SetMoonPhase(MoonPhase phase, bool shouldTriggerShift).
    /// Game enum: New = 1, Full = 2, None = 4.
    /// </summary>
    public static class MoonPhasePatch
    {
        private const int PHASE_NEW = 1;
        private const int PHASE_FULL = 2;
        private const int PHASE_NONE = 4;

        private static int _lastPhase = -1;
        private static float _lastTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var playerMgrType = AccessTools.TypeByName("PlayerManager");
                if (playerMgrType == null) return;

                MethodInfo method = null;
                foreach (var m in playerMgrType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name == "SetMoonPhase")
                    {
                        method = m;
                        break;
                    }
                }

                if (method != null)
                {
                    var prefix = new HarmonyMethod(typeof(MoonPhasePatch).GetMethod(nameof(Prefix)));
                    harmony.Patch(method, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched PlayerManager.SetMoonPhase");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("PlayerManager.SetMoonPhase not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SetMoonPhase: {ex.Message}");
            }
        }

        // __0 = MoonPhase enum (int-backed)
        public static void Prefix(object __0)
        {
            try
            {
                if (__0 == null) return;

                int phase;
                try { phase = Convert.ToInt32(__0); }
                catch { return; }

                if (phase == PHASE_NONE) return;

                float now = UnityEngine.Time.unscaledTime;
                if (phase == _lastPhase && now - _lastTime < 0.3f) return;
                _lastPhase = phase;
                _lastTime = now;

                string phaseName = phase == PHASE_NEW ? "New Moon" : phase == PHASE_FULL ? "Full Moon" : null;
                if (phaseName == null) return;

                MonsterTrainAccessibility.BattleHandler?.OnMoonPhaseChanged(phaseName);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MoonPhase patch: {ex.Message}");
            }
        }
    }
}
