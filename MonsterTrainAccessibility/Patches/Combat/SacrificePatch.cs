using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when a unit is sacrificed (distinct from regular death).
    /// Hooks CharacterState.Sacrifice(CardState, bool, bool, CharacterState).
    /// This is a coroutine (IEnumerator), so we use a prefix to capture the info.
    /// </summary>
    public static class SacrificePatch
    {
        private static string _lastAnnounced = "";
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                var method = AccessTools.Method(characterType, "Sacrifice");
                if (method != null)
                {
                    var prefix = new HarmonyMethod(typeof(SacrificePatch).GetMethod(nameof(Prefix)));
                    harmony.Patch(method, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.Sacrifice");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CharacterState.Sacrifice not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping sacrifice patch: {ex.Message}");
            }
        }

        public static void Prefix(object __instance)
        {
            try
            {
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);
                bool isEnemy = CharacterStateHelper.IsEnemyUnit(__instance);

                string key = $"{unitName}";
                float now = UnityEngine.Time.unscaledTime;
                if (key == _lastAnnounced && now - _lastAnnouncedTime < 0.5f) return;
                _lastAnnounced = key;
                _lastAnnouncedTime = now;

                MonsterTrainAccessibility.BattleHandler?.OnUnitSacrificed(unitName, isEnemy);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in sacrifice patch: {ex.Message}");
            }
        }
    }
}
