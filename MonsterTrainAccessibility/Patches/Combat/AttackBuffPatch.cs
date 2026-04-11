using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect when a unit's attack damage is buffed/debuffed.
    /// Hooks into CharacterState.BuffDamage or similar methods.
    /// </summary>
    public static class AttackBuffPatch
    {
        private static string _lastAnnounced = "";
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                MethodInfo method = null;
                var candidates = new[] { "BuffDamage", "AddDamage", "ModifyAttackDamage", "SetAttackDamage" };

                foreach (var name in candidates)
                {
                    var methods = characterType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var m in methods)
                    {
                        if (m.Name == name && m.GetParameters().Length >= 1)
                        {
                            method = m;
                            break;
                        }
                    }
                    if (method != null) break;
                }

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(AttackBuffPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched CharacterState.{method.Name} for attack buff announcements");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("BuffDamage method not found - attack buff announcements disabled");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping attack buff patch: {ex.Message}");
            }
        }

        public static void Postfix(object __instance, int __0)
        {
            try
            {
                if (__0 == 0) return;

                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);
                int amount = __0;

                // Deduplication
                string key = $"{unitName}_{amount}";
                float currentTime = UnityEngine.Time.unscaledTime;
                if (key == _lastAnnounced && currentTime - _lastAnnouncedTime < 0.5f)
                    return;

                _lastAnnounced = key;
                _lastAnnouncedTime = currentTime;

                MonsterTrainAccessibility.BattleHandler?.OnAttackBuffed(unitName, amount);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in attack buff patch: {ex.Message}");
            }
        }
    }
}
