using HarmonyLib;
using System;
using MonsterTrainAccessibility.Patches;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect when a unit's max HP is buffed.
    /// Hooks CharacterState.BuffMaxHP(int amount, ...).
    /// </summary>
    public static class MaxHPBuffPatch
    {
        private static float _lastBuffTime = 0f;
        private static string _lastBuffKey = "";

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                var method = AccessTools.Method(characterType, "BuffMaxHP");
                if (method != null)
                {
                    var prefix = new HarmonyMethod(typeof(MaxHPBuffPatch).GetMethod(nameof(Prefix)));
                    harmony.Patch(method, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.BuffMaxHP");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch BuffMaxHP: {ex.Message}");
            }
        }

        // __instance = CharacterState, __0 = amount (int)
        public static void Prefix(object __instance, int __0)
        {
            try
            {
                if (__0 <= 0 || __instance == null) return;

                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);

                // Deduplicate
                float currentTime = UnityEngine.Time.unscaledTime;
                string buffKey = $"{unitName}_{__0}";
                if (buffKey == _lastBuffKey && currentTime - _lastBuffTime < 0.3f)
                    return;

                _lastBuffKey = buffKey;
                _lastBuffTime = currentTime;

                MonsterTrainAccessibility.BattleHandler?.OnMaxHPBuffed(unitName, __0);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in max HP buff patch: {ex.Message}");
            }
        }
    }
}
