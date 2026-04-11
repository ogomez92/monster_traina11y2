using HarmonyLib;
using System;
using MonsterTrainAccessibility.Patches;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Mirror of MaxHPBuffPatch for max HP reductions.
    /// Hooks CharacterState.DebuffMaxHP(int amount, int floor, bool decreaseHp).
    /// </summary>
    public static class MaxHPDebuffPatch
    {
        private static float _lastDebuffTime = 0f;
        private static string _lastDebuffKey = "";

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                var method = AccessTools.Method(characterType, "DebuffMaxHP");
                if (method != null)
                {
                    var prefix = new HarmonyMethod(typeof(MaxHPDebuffPatch).GetMethod(nameof(Prefix)));
                    harmony.Patch(method, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.DebuffMaxHP");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DebuffMaxHP: {ex.Message}");
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

                float currentTime = UnityEngine.Time.unscaledTime;
                string debuffKey = $"{unitName}_{__0}_maxhp_debuff";
                if (debuffKey == _lastDebuffKey && currentTime - _lastDebuffTime < 0.3f)
                    return;

                _lastDebuffKey = debuffKey;
                _lastDebuffTime = currentTime;

                MonsterTrainAccessibility.BattleHandler?.OnMaxHPDebuffed(unitName, __0);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in max HP debuff patch: {ex.Message}");
            }
        }
    }
}
