using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect when a unit is spawned (added to the game board)
    /// CharacterState.Setup is the most reliable, but name isn't available yet
    /// We use the Setup parameters to get CharacterData and read the name from there
    /// </summary>
    public static class UnitSpawnPatch
    {
        // Track announced spawns to avoid duplicates
        private static HashSet<int> _announcedSpawns = new HashSet<int>();
        private static float _lastClearTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // MonsterManager/HeroManager don't have InstantiateCharacter in MT2.
                // Use CharacterState.Setup which fires for all spawned characters.
                // Setup signature: (CharacterStateSetup setup, SpawnPoint startingSpawnPoint, SetupVfxData, bool createInPreviewState)
                var characterStateType = AccessTools.TypeByName("CharacterState");
                if (characterStateType != null)
                {
                    var method = AccessTools.Method(characterStateType, "Setup");
                    if (method != null)
                    {
                        // Log the parameters so we know what's available
                        var parameters = method.GetParameters();
                        MonsterTrainAccessibility.LogInfo($"CharacterState.Setup has {parameters.Length} parameters:");
                        foreach (var p in parameters)
                        {
                            MonsterTrainAccessibility.LogInfo($"  {p.Name}: {p.ParameterType.Name}");
                        }

                        // Setup is IEnumerator, use prefix to run before the coroutine
                        // Actually, we want postfix to read state AFTER setup completes
                        var postfix = new HarmonyMethod(typeof(UnitSpawnPatch).GetMethod(nameof(PostfixCharacterSetup)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CharacterState.Setup");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch unit spawn: {ex.Message}");
            }
        }

        // CharacterState.Setup(CharacterStateSetup setup, SpawnPoint startingSpawnPoint,
        //                      SetupVfxData vfx, bool createInPreviewState)
        public static void PostfixCharacterSetup(object __instance, object __0, object __3)
        {
            try
            {
                // Suppress preview-mode spawns. The game spawns preview CharacterStates
                // whenever the player hovers a unit card to show where it would land —
                // these aren't real summons and shouldn't be announced.
                if (__3 is bool createInPreview && createInPreview)
                    return;
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                // Clear old spawn tracking periodically
                float currentTime = UnityEngine.Time.unscaledTime;
                if (currentTime - _lastClearTime > 10f)
                {
                    _announcedSpawns.Clear();
                    _lastClearTime = currentTime;
                }

                // Use instance hash to track duplicates - check early
                int hash = __instance.GetHashCode();
                if (_announcedSpawns.Contains(hash))
                {
                    return;
                }

                string name = null;
                bool isEnemy = IsEnemyUnit(__instance);
                int roomIndex = -1;

                // After Setup completes, the instance should have the name available
                // Try GetName on the instance first (most reliable after setup)
                var instanceType = __instance.GetType();
                var getNameMethod = instanceType.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(__instance, null) as string;
                }

                // If instance GetName failed, try the first parameter (CharacterData)
                if ((string.IsNullOrEmpty(name) || name == "Unit" || name.Contains("KEY>")) && __0 != null)
                {
                    var paramType = __0.GetType();
                    var paramGetNameMethod = paramType.GetMethod("GetName");
                    if (paramGetNameMethod != null)
                    {
                        var paramName = paramGetNameMethod.Invoke(__0, null) as string;
                        if (!string.IsNullOrEmpty(paramName) && paramName != "Unit" && !paramName.Contains("KEY>"))
                        {
                            name = paramName;
                        }
                    }
                }

                // Get room index from instance
                roomIndex = GetRoomIndex(__instance);

                // Skip invalid names
                if (string.IsNullOrEmpty(name) || name == "Unit" || name.Contains("KEY>"))
                {
                    MonsterTrainAccessibility.LogInfo($"CharacterState.Setup: skipping invalid name '{name}'");
                    return;
                }

                // Track this spawn
                _announcedSpawns.Add(hash);

                int userFloor = RoomIndexToUserFloor(roomIndex);

                // Build a proper announcement
                string floorText = userFloor > 0 ? $"floor {userFloor}" : "the battlefield";
                string unitType = isEnemy ? "Enemy" : "Friendly";

                MonsterTrainAccessibility.LogInfo($"Unit spawned via Setup: {name}, isEnemy={isEnemy}, roomIndex={roomIndex}, floor={userFloor}");

                // Announce the spawn with proper floor text
                MonsterTrainAccessibility.BattleHandler?.OnUnitSpawned(name, isEnemy, userFloor);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in character setup patch: {ex.Message}");
            }
        }

        private static string GetUnitName(object characterState)
        {
            try
            {
                var type = characterState.GetType();

                // Try GetName first
                var getNameMethod = type.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(characterState, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
                }

                // Try GetSourceCharacterData / GetCharacterData
                var getDataMethod = type.GetMethod("GetSourceCharacterData") ?? type.GetMethod("GetCharacterData");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(characterState, null);
                    if (data != null)
                    {
                        var dataGetNameMethod = data.GetType().GetMethod("GetName");
                        if (dataGetNameMethod != null)
                        {
                            var name = dataGetNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch { }
            return "Unit";
        }

        private static int GetRoomIndex(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getRoomMethod = type.GetMethod("GetCurrentRoomIndex");
                if (getRoomMethod != null)
                {
                    var result = getRoomMethod.Invoke(characterState, null);
                    if (result is int index)
                        return index;
                }
            }
            catch { }
            return -1;
        }

        private static bool IsEnemyUnit(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getTeamMethod = type.GetMethod("GetTeamType");
                if (getTeamMethod != null)
                {
                    var team = getTeamMethod.Invoke(characterState, null);
                    return team?.ToString() == "Heroes";
                }
            }
            catch { }
            return false;
        }

        private static int RoomIndexToUserFloor(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex > 2) return -1;
            return roomIndex + 1;
        }
    }
}
