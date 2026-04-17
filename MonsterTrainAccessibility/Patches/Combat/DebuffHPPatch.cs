using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when a unit's HP is directly debuffed (not via damage).
    /// Hooks CharacterState.DebuffHP(int amount).
    /// This is distinct from MaxHPDebuff - it reduces current HP directly.
    /// </summary>
    public static class DebuffHPPatch
    {
        private static string _lastAnnounced = "";
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                var method = AccessTools.Method(characterType, "DebuffHP", new[] { typeof(int) });
                if (method != null)
                {
                    var prefix = new HarmonyMethod(typeof(DebuffHPPatch).GetMethod(nameof(Prefix)));
                    harmony.Patch(method, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.DebuffHP");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CharacterState.DebuffHP not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping DebuffHP patch: {ex.Message}");
            }
        }

        public static void Prefix(object __instance, int __0)
        {
            try
            {
                if (__0 <= 0) return;

                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);

                string key = $"{unitName}_{__0}";
                float now = UnityEngine.Time.unscaledTime;
                if (key == _lastAnnounced && now - _lastAnnouncedTime < 0.5f) return;
                _lastAnnounced = key;
                _lastAnnouncedTime = now;

                MonsterTrainAccessibility.BattleHandler?.OnHPDebuffed(unitName, __0);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DebuffHP patch: {ex.Message}");
            }
        }
    }
}
