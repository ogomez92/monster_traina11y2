using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when energy (ember) is modified for next turn or every turn.
    /// Hooks CombatManager.ModifyEnergyForNextTurn and ModifyEnergyForEveryTurn.
    /// </summary>
    public static class EnergyModifiedPatch
    {
        private static string _lastAnnounced = "";
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType == null) return;

                PatchMethod(harmony, combatType, "ModifyEnergyForNextTurn", nameof(NextTurnPostfix));
                PatchMethod(harmony, combatType, "ModifyEnergyForEveryTurn", nameof(EveryTurnPostfix));
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping energy modified patch: {ex.Message}");
            }
        }

        private static void PatchMethod(Harmony harmony, Type combatType, string methodName, string postfixName)
        {
            try
            {
                var method = AccessTools.Method(combatType, methodName);
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(EnergyModifiedPatch).GetMethod(postfixName));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched CombatManager.{methodName}");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Could not patch {methodName}: {ex.Message}");
            }
        }

        public static void NextTurnPostfix(int __0)
        {
            try
            {
                if (__0 == 0) return;

                string key = $"next_{__0}";
                float now = UnityEngine.Time.unscaledTime;
                if (key == _lastAnnounced && now - _lastAnnouncedTime < 0.3f) return;
                _lastAnnounced = key;
                _lastAnnouncedTime = now;

                string verb = __0 > 0 ? "more" : "less";
                MonsterTrainAccessibility.BattleHandler?.OnEnergyModified(__0, false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in energy next turn patch: {ex.Message}");
            }
        }

        public static void EveryTurnPostfix(int __0)
        {
            try
            {
                if (__0 == 0) return;

                string key = $"every_{__0}";
                float now = UnityEngine.Time.unscaledTime;
                if (key == _lastAnnounced && now - _lastAnnouncedTime < 0.3f) return;
                _lastAnnounced = key;
                _lastAnnouncedTime = now;

                MonsterTrainAccessibility.BattleHandler?.OnEnergyModified(__0, true);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in energy every turn patch: {ex.Message}");
            }
        }
    }
}
