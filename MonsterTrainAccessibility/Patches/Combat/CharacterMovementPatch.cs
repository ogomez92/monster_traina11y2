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

        // Cross-patch dedup: SpawnPointChangedPatch checks these so we don't
        // double-announce when a unit ascends (which fires both PostAscension
        // and OnSpawnPointChanged in the same frame).
        public static int LastAnnouncedHash;
        public static float LastAnnouncedTime;

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

        // Read the unit's current room via CharacterState.GetCurrentRoomIndex() and
        // convert to user-facing floor (room 0=floor 3 top, 2=floor 1 bottom, 3=pyre→0).
        // Returns -1 if the room can't be resolved so the caller can drop the suffix.
        private static System.Reflection.MethodInfo _getCurrentRoomIndexMethod;
        private static int GetUserFloorFromCharacter(object characterState)
        {
            if (characterState == null) return -1;
            try
            {
                if (_getCurrentRoomIndexMethod == null || _getCurrentRoomIndexMethod.DeclaringType != characterState.GetType())
                {
                    _getCurrentRoomIndexMethod = characterState.GetType().GetMethod("GetCurrentRoomIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (_getCurrentRoomIndexMethod == null)
                    {
                        MonsterTrainAccessibility.LogWarning($"CharacterMovementPatch: GetCurrentRoomIndex not found on {characterState.GetType().FullName}");
                        return -1;
                    }
                }
                var result = _getCurrentRoomIndexMethod.Invoke(characterState, null);
                int roomIndex = Convert.ToInt32(result);
                if (roomIndex < 0 || roomIndex > 3)
                {
                    MonsterTrainAccessibility.LogWarning($"CharacterMovementPatch: out-of-range roomIndex={roomIndex}");
                    return -1;
                }
                return roomIndex == 3 ? 0 : roomIndex + 1; // pyre → 0, otherwise room 0=bottom (1), 1=mid (2), 2=top (3)
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetUserFloorFromCharacter error: {ex}");
                return -1;
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
                int destinationFloor = GetUserFloorFromCharacter(__0);

                string key = $"{unitName}_{triggerValue}_{destinationFloor}";
                float now = UnityEngine.Time.unscaledTime;
                if (key == _lastKey && now - _lastTime < 0.3f) return;
                _lastKey = key;
                _lastTime = now;

                LastAnnouncedHash = __0.GetHashCode();
                LastAnnouncedTime = now;
                MonsterTrainAccessibility.BattleHandler?.OnCharacterMoved(unitName, ascended, destinationFloor);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in movement patch: {ex.Message}");
            }
        }
    }
}
