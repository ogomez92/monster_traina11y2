using HarmonyLib;
using System;
using MonsterTrainAccessibility.Patches;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect when a status effect is removed from a unit.
    /// Hooks CharacterState.RemoveStatusEffect(string statusId, ...)
    /// </summary>
    public static class StatusEffectRemovedPatch
    {
        private static string _lastRemovedEffect = "";
        private static float _lastRemovedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                var method = AccessTools.Method(characterType, "RemoveStatusEffect");
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(StatusEffectRemovedPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.RemoveStatusEffect");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RemoveStatusEffect: {ex.Message}");
            }
        }

        // __instance = CharacterState, __0 = string statusId, __1 = int numStacks, __2 = bool allowModification
        // Real signature: RemoveStatusEffect(string statusId, int numStacks, bool allowModification = true)
        public static void Postfix(object __instance, string __0, int __1)
        {
            try
            {
                if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                    return;

                if (string.IsNullOrEmpty(__0) || __1 <= 0)
                    return;

                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);
                string effectName = CharacterStateHelper.CleanStatusName(__0);

                // Deduplicate
                string effectKey = $"{unitName}_{__0}_{__1}_remove";
                float currentTime = UnityEngine.Time.unscaledTime;
                if (effectKey == _lastRemovedEffect && currentTime - _lastRemovedTime < 0.5f)
                    return;

                _lastRemovedEffect = effectKey;
                _lastRemovedTime = currentTime;

                MonsterTrainAccessibility.BattleHandler?.OnStatusEffectRemoved(unitName, effectName, __1);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in status effect removed patch: {ex.Message}");
            }
        }
    }
}
