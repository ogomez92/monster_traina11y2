using HarmonyLib;
using System;
using MonsterTrainAccessibility.Patches;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect when an artifact/relic triggers during combat.
    /// Hooks RelicManager.NotifyRelicTriggered(RelicState, IRelicEffect)
    /// </summary>
    public static class RelicTriggeredPatch
    {
        private static float _lastTriggerTime = 0f;
        private static string _lastTriggerKey = "";

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var relicManagerType = AccessTools.TypeByName("RelicManager");
                if (relicManagerType == null) return;

                var method = AccessTools.Method(relicManagerType, "NotifyRelicTriggered");
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RelicTriggeredPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched RelicManager.NotifyRelicTriggered");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch NotifyRelicTriggered: {ex.Message}");
            }
        }

        // __0 = RelicState triggeredRelic, __1 = IRelicEffect triggeredEffect
        public static void Postfix(object __0, object __1)
        {
            try
            {
                if (__0 == null) return;

                string relicName = CharacterStateHelper.GetRelicName(__0);

                // Deduplicate rapid triggers of the same relic
                float currentTime = UnityEngine.Time.unscaledTime;
                if (relicName == _lastTriggerKey && currentTime - _lastTriggerTime < 0.3f)
                    return;

                _lastTriggerKey = relicName;
                _lastTriggerTime = currentTime;

                MonsterTrainAccessibility.BattleHandler?.OnRelicTriggered(relicName);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in relic triggered patch: {ex.Message}");
            }
        }
    }
}
