using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when a card upgrade is applied to a unit in battle.
    /// Hooks CharacterState.ApplyCardUpgrade (IEnumerator, so prefix).
    /// Announces stat changes from upgrades like +attack, +HP, +size.
    /// </summary>
    public static class CardUpgradeAppliedPatch
    {
        private static string _lastAnnounced = "";
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                var method = AccessTools.Method(characterType, "ApplyCardUpgrade");
                if (method != null)
                {
                    var prefix = new HarmonyMethod(typeof(CardUpgradeAppliedPatch).GetMethod(nameof(Prefix)));
                    harmony.Patch(method, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.ApplyCardUpgrade");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CharacterState.ApplyCardUpgrade not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping ApplyCardUpgrade patch: {ex.Message}");
            }
        }

        // __instance = CharacterState, __0 = CardUpgradeState
        public static void Prefix(object __instance, object __0)
        {
            try
            {
                if (__0 == null) return;

                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);
                string upgradeSummary = GetUpgradeSummary(__0);

                if (string.IsNullOrEmpty(upgradeSummary)) return;

                string key = $"{unitName}_{upgradeSummary}";
                float now = UnityEngine.Time.unscaledTime;
                if (key == _lastAnnounced && now - _lastAnnouncedTime < 0.5f) return;
                _lastAnnounced = key;
                _lastAnnouncedTime = now;

                MonsterTrainAccessibility.BattleHandler?.OnCardUpgradeApplied(unitName, upgradeSummary);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ApplyCardUpgrade patch: {ex.Message}");
            }
        }

        private static string GetUpgradeSummary(object cardUpgradeState)
        {
            try
            {
                var type = cardUpgradeState.GetType();
                var parts = new System.Collections.Generic.List<string>();

                int attack = GetIntMethod(type, cardUpgradeState, "GetAttackDamage");
                if (attack != 0) parts.Add($"{FormatDelta(attack)} ATK");

                int hp = GetIntMethod(type, cardUpgradeState, "GetAdditionalHP");
                if (hp != 0) parts.Add($"{FormatDelta(hp)} HP");

                int size = GetIntMethod(type, cardUpgradeState, "GetAdditionalSize");
                if (size != 0) parts.Add($"{FormatDelta(size)} SIZE");

                int attackBuff = GetIntMethod(type, cardUpgradeState, "GetAttackDamageBuff");
                if (attackBuff != 0) parts.Add($"{FormatDelta(attackBuff)} ATK");

                if (parts.Count == 0) return null;
                return string.Join(", ", parts);
            }
            catch { }
            return null;
        }

        private static int GetIntMethod(Type type, object instance, string methodName)
        {
            try
            {
                var method = type.GetMethod(methodName, Type.EmptyTypes);
                if (method != null)
                {
                    var result = method.Invoke(instance, null);
                    if (result is int val) return val;
                }
            }
            catch { }
            return 0;
        }

        private static string FormatDelta(int value)
        {
            return value > 0 ? $"+{value}" : value.ToString();
        }
    }
}
