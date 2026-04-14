using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce unit spawns by hooking RoomManager.OnSpawnPointChanged. By the time
    /// this fires, the character is fully initialized — names are resolved, team is
    /// set, room index is final. This succeeds where UnitSpawnPatch (CharacterState.Setup)
    /// fails, because Setup runs before localization is resolved and we get
    /// "KEY>>NULL&lt;&lt;" for the name.
    ///
    /// Detects fresh spawns by checking that prevPoint is null (i.e. unit is appearing,
    /// not moving between spawn points).
    /// </summary>
    public static class SpawnPointChangedPatch
    {
        private static readonly System.Collections.Generic.HashSet<int> _announced =
            new System.Collections.Generic.HashSet<int>();
        private static float _lastClearTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var rmType = AccessTools.TypeByName("RoomManager");
                if (rmType == null)
                {
                    MonsterTrainAccessibility.LogWarning("SpawnPointChangedPatch: RoomManager type not found");
                    return;
                }

                var method = AccessTools.Method(rmType, "OnSpawnPointChanged");
                if (method == null)
                {
                    MonsterTrainAccessibility.LogWarning("SpawnPointChangedPatch: OnSpawnPointChanged not found");
                    return;
                }

                var postfix = new HarmonyMethod(typeof(SpawnPointChangedPatch).GetMethod(nameof(Postfix)));
                harmony.Patch(method, postfix: postfix);
                MonsterTrainAccessibility.LogInfo("Patched RoomManager.OnSpawnPointChanged");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"SpawnPointChangedPatch.TryPatch failed: {ex}");
            }
        }

        // OnSpawnPointChanged(CharacterState characterState, SpawnPoint prevPoint, SpawnPoint newPoint)
        public static void Postfix(object __0, object __1, object __2)
        {
            try
            {
                if (__0 == null || __2 == null) return;

                var battle = MonsterTrainAccessibility.BattleHandler;
                if (battle == null || !battle.IsInBattle) return;

                bool isFreshSpawn = __1 == null;
                int newRoom = GetRoomIndex(__0);
                int prevRoom = isFreshSpawn ? -1 : GetRoomFromSpawnPoint(__1);

                // For movements within the same room (slot rearrange), don't announce.
                if (!isFreshSpawn && prevRoom == newRoom) return;

                string name = GetUnitName(__0);
                if (string.IsNullOrEmpty(name) || name == "Unit" || name.Contains("KEY>"))
                {
                    MonsterTrainAccessibility.LogInfo($"SpawnPointChangedPatch: skipping invalid name '{name}'");
                    return;
                }

                bool isEnemy = IsEnemyUnit(__0);
                int userFloor = newRoom == 3 ? 0 : (newRoom >= 0 && newRoom <= 2 ? newRoom + 1 : -1);

                if (isFreshSpawn)
                {
                    // Periodically clear the dedup set so we don't leak hashes across battles
                    float now = UnityEngine.Time.unscaledTime;
                    if (now - _lastClearTime > 30f)
                    {
                        _announced.Clear();
                        _lastClearTime = now;
                    }
                    int hash = __0.GetHashCode();
                    if (_announced.Contains(hash)) return;
                    _announced.Add(hash);

                    MonsterTrainAccessibility.LogInfo($"SpawnPointChanged spawn: {name}, enemy={isEnemy}, room={newRoom}, floor={userFloor}");
                    battle.OnUnitSpawned(name, isEnemy, userFloor);
                }
                else
                {
                    // Movement between floors (bump, push, etc.). Skip if CharacterMovementPatch
                    // already announced this character's ascend/descend within the last 0.5s.
                    int hash = __0.GetHashCode();
                    float now = UnityEngine.Time.unscaledTime;
                    if (CharacterMovementPatch.LastAnnouncedHash == hash &&
                        now - CharacterMovementPatch.LastAnnouncedTime < 0.5f)
                        return;

                    bool ascended = newRoom > prevRoom; // higher room index = higher floor (top)
                    MonsterTrainAccessibility.LogInfo($"SpawnPointChanged move: {name}, room {prevRoom}→{newRoom}, ascended={ascended}");
                    battle.OnCharacterMoved(name, ascended, userFloor);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"SpawnPointChangedPatch.Postfix error: {ex}");
            }
        }

        private static int GetRoomFromSpawnPoint(object spawnPoint)
        {
            try
            {
                var getOwnerMethod = spawnPoint.GetType().GetMethod("GetRoomOwner", Type.EmptyTypes);
                var roomState = getOwnerMethod?.Invoke(spawnPoint, null);
                if (roomState == null) return -1;

                // RoomState has GetRoomIndex() or similar. Try a few names.
                var roomType = roomState.GetType();
                foreach (var name in new[] { "GetRoomIndex", "GetIndex", "RoomIndex" })
                {
                    var m = roomType.GetMethod(name, Type.EmptyTypes);
                    if (m != null)
                    {
                        var r = m.Invoke(roomState, null);
                        if (r is int idx) return idx;
                    }
                }
                // Fall back to property
                var prop = roomType.GetProperty("RoomIndex");
                if (prop != null)
                {
                    var r = prop.GetValue(roomState);
                    if (r is int idx) return idx;
                }
            }
            catch { }
            return -1;
        }

        private static string GetUnitName(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getNameMethod = type.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(characterState, null) as string;
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            catch { }
            return null;
        }

        private static bool IsEnemyUnit(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getTeamMethod = type.GetMethod("GetTeamType", Type.EmptyTypes);
                if (getTeamMethod != null)
                {
                    var team = getTeamMethod.Invoke(characterState, null);
                    return team?.ToString() == "Heroes";
                }
            }
            catch { }
            return false;
        }

        private static int GetRoomIndex(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getRoomMethod = type.GetMethod("GetCurrentRoomIndex", Type.EmptyTypes);
                if (getRoomMethod != null)
                {
                    var result = getRoomMethod.Invoke(characterState, null);
                    if (result is int idx) return idx;
                }
            }
            catch { }
            return -1;
        }
    }
}
