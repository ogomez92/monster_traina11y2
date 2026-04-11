using HarmonyLib;
using System;
using MonsterTrainAccessibility.Patches;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when a character ascends or descends between floors.
    /// Hooks CombatManager.QueueTrigger(CharacterState, CharacterTriggerData.Trigger, ...) and
    /// filters for PostAscension / PostDescension enum values (13 and 17 per game source).
    /// Covers both friendly and enemy movement, since HeroManager and monster code both queue
    /// these triggers through CombatManager.
    /// </summary>
    public static class CharacterMovementPatch
    {
        // CharacterTriggerData.Trigger values from game source:
        //   PostAscension = 13, PostDescension = 17
        //   PostAttemptedAscension / PostAttemptedDescension are other ints we ignore
        private const int TRIGGER_POST_ASCENSION = 13;
        private const int TRIGGER_POST_DESCENSION = 17;

        private static string _lastKey = "";
        private static float _lastTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType == null) return;

                // QueueTrigger has two overloads; we want the one that takes a CharacterState first.
                System.Reflection.MethodInfo method = null;
                foreach (var m in combatType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (m.Name != "QueueTrigger") continue;
                    var ps = m.GetParameters();
                    if (ps.Length >= 2 && ps[0].ParameterType.Name == "CharacterState")
                    {
                        method = m;
                        break;
                    }
                }

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(CharacterMovementPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CombatManager.QueueTrigger for movement announcements");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CombatManager.QueueTrigger(CharacterState, ...) not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch QueueTrigger: {ex.Message}");
            }
        }

        // __0 = CharacterState, __1 = CharacterTriggerData.Trigger enum
        public static void Postfix(object __0, object __1)
        {
            try
            {
                if (__0 == null || __1 == null) return;

                if (PreviewModeDetector.ShouldSuppressAnnouncement(__0))
                    return;

                int triggerValue;
                try { triggerValue = Convert.ToInt32(__1); }
                catch { return; }

                bool ascended = triggerValue == TRIGGER_POST_ASCENSION;
                bool descended = triggerValue == TRIGGER_POST_DESCENSION;
                if (!ascended && !descended) return;

                string unitName = CharacterStateHelper.GetUnitName(__0);

                string key = $"{unitName}_{triggerValue}";
                float now = UnityEngine.Time.unscaledTime;
                if (key == _lastKey && now - _lastTime < 0.3f) return;
                _lastKey = key;
                _lastTime = now;

                MonsterTrainAccessibility.BattleHandler?.OnCharacterMoved(unitName, ascended);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in movement patch: {ex.Message}");
            }
        }
    }
}
