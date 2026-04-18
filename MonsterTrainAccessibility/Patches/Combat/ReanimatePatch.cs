using HarmonyLib;
using MonsterTrainAccessibility.Patches;
using System;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when a unit with Undying reanimates (comes back at 1 HP).
    /// Hooks CharacterState.ReviveFromUndyingStatus — the only path used by the
    /// undying status effect to revive a "dead" character.
    /// </summary>
    public static class ReanimatePatch
    {
        private static int _lastHash;
        private static float _lastTime;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var charType = AccessTools.TypeByName("CharacterState");
                if (charType == null) return;

                var method = AccessTools.Method(charType, "ReviveFromUndyingStatus");
                if (method == null)
                {
                    MonsterTrainAccessibility.LogInfo("CharacterState.ReviveFromUndyingStatus not found - reanimate announcements disabled");
                    return;
                }

                var prefix = new HarmonyMethod(typeof(ReanimatePatch).GetMethod(nameof(Prefix)));
                harmony.Patch(method, prefix: prefix);
                MonsterTrainAccessibility.LogInfo("Patched CharacterState.ReviveFromUndyingStatus for reanimate announcements");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ReviveFromUndyingStatus: {ex.Message}");
            }
        }

        public static void Prefix(object __instance)
        {
            try
            {
                if (__instance == null) return;
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance)) return;

                // The coroutine may be re-entered if multiple effects resolve in the same
                // frame; ignore back-to-back calls on the same instance.
                int hash = __instance.GetHashCode();
                float now = UnityEngine.Time.unscaledTime;
                if (hash == _lastHash && now - _lastTime < 0.3f) return;
                _lastHash = hash;
                _lastTime = now;

                string unitName = CharacterStateHelper.GetUnitName(__instance);
                MonsterTrainAccessibility.BattleHandler?.OnUnitReanimated(unitName);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in reanimate patch: {ex.Message}");
            }
        }
    }
}
