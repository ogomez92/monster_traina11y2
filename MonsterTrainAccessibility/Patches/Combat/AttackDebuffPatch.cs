using HarmonyLib;
using System;
using System.Reflection;
using MonsterTrainAccessibility.Patches;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Mirror of AttackBuffPatch for attack reductions.
    /// Hooks CharacterState.DebuffDamage(int amount, RelicState, bool fromStatusEffect).
    /// </summary>
    public static class AttackDebuffPatch
    {
        private static string _lastAnnounced = "";
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                var method = AccessTools.Method(characterType, "DebuffDamage");
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(AttackDebuffPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.DebuffDamage for attack debuff announcements");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("DebuffDamage method not found - attack debuff announcements disabled");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping attack debuff patch: {ex.Message}");
            }
        }

        public static void Postfix(object __instance, int __0)
        {
            try
            {
                if (__0 <= 0) return;

                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);
                int amount = __0;

                string key = $"{unitName}_{amount}_debuff";
                float currentTime = UnityEngine.Time.unscaledTime;
                if (key == _lastAnnounced && currentTime - _lastAnnouncedTime < 0.5f)
                    return;

                _lastAnnounced = key;
                _lastAnnouncedTime = currentTime;

                MonsterTrainAccessibility.BattleHandler?.OnAttackDebuffed(unitName, amount);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in attack debuff patch: {ex.Message}");
            }
        }
    }
}
