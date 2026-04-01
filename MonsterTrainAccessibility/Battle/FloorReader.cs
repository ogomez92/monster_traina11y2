using System;
using System.Collections.Generic;
using System.Text;

namespace MonsterTrainAccessibility.Battle
{
    /// <summary>
    /// Reads floor/room information from the game's RoomManager via reflection.
    /// </summary>
    public class FloorReader
    {
        private readonly BattleManagerCache _cache;

        public FloorReader(BattleManagerCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Announce all floors
        /// </summary>
        public void AnnounceAllFloors()
        {
            try
            {
                var output = MonsterTrainAccessibility.ScreenReader;
                output?.Speak("Floor status:", false);

                // Monster Train has 3 playable floors + pyre room
                // Room indices: 0=top floor, 1=middle, 2=bottom, 3=pyre room
                // User floors: 1=bottom, 2=middle, 3=top
                // Iterate user floors from bottom (1) to top (3)
                for (int userFloor = 1; userFloor <= 3; userFloor++)
                {
                    int roomIndex = 3 - userFloor; // Convert user floor to room index
                    var room = GetRoom(roomIndex);
                    if (room != null)
                    {
                        string floorName = $"Floor {userFloor}";
                        var units = UnitInfoHelper.GetUnitsInRoom(room);

                        if (units.Count == 0)
                        {
                            output?.Queue($"{floorName}: Empty");
                        }
                        else
                        {
                            var descriptions = new List<string>();
                            foreach (var unit in units)
                            {
                                string name = UnitInfoHelper.GetUnitName(unit, _cache);
                                int hp = UnitInfoHelper.GetUnitHP(unit, _cache);
                                int attack = UnitInfoHelper.GetUnitAttack(unit, _cache);
                                bool isEnemy = UnitInfoHelper.IsEnemyUnit(unit, _cache);
                                string prefix = isEnemy ? "Enemy" : "";
                                descriptions.Add($"{prefix} {name} {attack}/{hp}");
                            }
                            output?.Queue($"{floorName}: {string.Join(", ", descriptions)}");
                        }
                    }
                }

                // Announce pyre health
                int pyreHP = GetPyreHealth();
                int maxPyreHP = GetMaxPyreHealth();
                if (pyreHP >= 0)
                {
                    output?.Queue($"Pyre: {pyreHP} of {maxPyreHP} health");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing floors: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read floors", false);
            }
        }

        /// <summary>
        /// Get the currently selected floor from the game state.
        /// Returns user-facing floor number (1-3, where 1 is bottom, 3 is top).
        /// Returns -1 if unable to determine.
        /// </summary>
        public int GetSelectedFloor()
        {
            try
            {
                if (_cache.RoomManager == null)
                {
                    _cache.FindManagers();
                }

                if (_cache.RoomManager == null)
                {
                    MonsterTrainAccessibility.LogInfo("GetSelectedFloor: RoomManager is null");
                    return -1;
                }

                var roomManagerType = _cache.RoomManager.GetType();
                int roomIndex = -1;

                // GetSelectedRoom() returns an int (room index) directly, not a RoomState object
                var getSelectedRoomMethod = roomManagerType.GetMethod("GetSelectedRoom", Type.EmptyTypes);
                if (getSelectedRoomMethod != null)
                {
                    var result = getSelectedRoomMethod.Invoke(_cache.RoomManager, null);
                    if (result is int idx)
                    {
                        roomIndex = idx;
                        MonsterTrainAccessibility.LogInfo($"GetSelectedFloor: GetSelectedRoom() = {roomIndex}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo($"GetSelectedFloor: GetSelectedRoom() returned {result?.GetType().Name ?? "null"}: {result}");
                    }
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("GetSelectedFloor: GetSelectedRoom method not found");
                }

                // Convert room index to user floor
                // Room 0 = Floor 3 (top), Room 1 = Floor 2, Room 2 = Floor 1 (bottom), Room 3 = Pyre (floor 0)
                if (roomIndex >= 0 && roomIndex <= 3)
                {
                    int userFloor = 3 - roomIndex; // This gives: 0->3, 1->2, 2->1, 3->0 (pyre)
                    MonsterTrainAccessibility.LogInfo($"GetSelectedFloor: Converting room {roomIndex} to floor {userFloor}");
                    return userFloor;
                }

                return -1;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetSelectedFloor error: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Get a room object by internal room index
        /// </summary>
        public object GetRoom(int roomIndex)
        {
            if (_cache.RoomManager == null || _cache.GetRoomMethod == null)
            {
                _cache.FindManagers();
                if (_cache.RoomManager == null)
                {
                    MonsterTrainAccessibility.LogInfo("GetRoom: RoomManager is null");
                    return null;
                }
                if (_cache.GetRoomMethod == null)
                {
                    MonsterTrainAccessibility.LogInfo("GetRoom: GetRoomMethod is null");
                    return null;
                }
            }

            try
            {
                var room = _cache.GetRoomMethod?.Invoke(_cache.RoomManager, new object[] { roomIndex });
                MonsterTrainAccessibility.LogInfo($"GetRoom({roomIndex}): {(room != null ? room.GetType().Name : "null")}");
                return room;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetRoom({roomIndex}) error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get a text summary of what's on a specific floor (for floor targeting).
        /// Floor numbers are 1-3 where 1 is bottom, 3 is top (closest to pyre).
        /// </summary>
        public string GetFloorSummary(int floorNumber)
        {
            try
            {
                // Convert user-facing floor number (1-3) to internal room index
                int roomIndex = 3 - floorNumber;

                var room = GetRoom(roomIndex);
                if (room == null)
                {
                    return $"Floor {floorNumber}: Unknown";
                }

                var units = UnitInfoHelper.GetUnitsInRoom(room);
                if (units.Count == 0)
                {
                    return "Empty";
                }

                var friendlyUnits = new List<string>();
                var enemyUnits = new List<string>();

                foreach (var unit in units)
                {
                    string name = UnitInfoHelper.GetUnitName(unit, _cache);
                    int hp = UnitInfoHelper.GetUnitHP(unit, _cache);
                    int attack = UnitInfoHelper.GetUnitAttack(unit, _cache);
                    string description = $"{name} {attack}/{hp}";

                    if (UnitInfoHelper.IsEnemyUnit(unit, _cache))
                    {
                        enemyUnits.Add(description);
                    }
                    else
                    {
                        friendlyUnits.Add(description);
                    }
                }

                var parts = new List<string>();
                if (friendlyUnits.Count > 0)
                {
                    parts.Add($"Your units: {string.Join(", ", friendlyUnits)}");
                }
                if (enemyUnits.Count > 0)
                {
                    parts.Add($"Enemies: {string.Join(", ", enemyUnits)}");
                }

                return string.Join(". ", parts);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting floor summary: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Get a list of all enemy units on all floors.
        /// Returns a list of formatted strings like "Armored Shiv 10/20 on floor 2"
        /// </summary>
        public List<string> GetAllEnemies()
        {
            var enemies = new List<string>();
            try
            {
                for (int floorNumber = 1; floorNumber <= 3; floorNumber++)
                {
                    int roomIndex = 3 - floorNumber;
                    var room = GetRoom(roomIndex);
                    if (room == null) continue;

                    var units = UnitInfoHelper.GetUnitsInRoom(room);
                    foreach (var unit in units)
                    {
                        if (UnitInfoHelper.IsEnemyUnit(unit, _cache))
                        {
                            string name = UnitInfoHelper.GetUnitName(unit, _cache);
                            int hp = UnitInfoHelper.GetUnitHP(unit, _cache);
                            int attack = UnitInfoHelper.GetUnitAttack(unit, _cache);
                            enemies.Add($"{name} {attack}/{hp} on floor {floorNumber}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting all enemies: {ex.Message}");
            }
            return enemies;
        }

        /// <summary>
        /// Get a list of all friendly units on all floors.
        /// Returns a list of formatted strings like "Train Steward 5/8 on floor 1"
        /// </summary>
        public List<string> GetAllFriendlyUnits()
        {
            var friendlies = new List<string>();
            try
            {
                for (int floorNumber = 1; floorNumber <= 3; floorNumber++)
                {
                    int roomIndex = 3 - floorNumber;
                    var room = GetRoom(roomIndex);
                    if (room == null) continue;

                    var units = UnitInfoHelper.GetUnitsInRoom(room);
                    foreach (var unit in units)
                    {
                        if (!UnitInfoHelper.IsEnemyUnit(unit, _cache))
                        {
                            string name = UnitInfoHelper.GetUnitName(unit, _cache);
                            int hp = UnitInfoHelper.GetUnitHP(unit, _cache);
                            int attack = UnitInfoHelper.GetUnitAttack(unit, _cache);
                            friendlies.Add($"{name} {attack}/{hp} on floor {floorNumber}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting friendly units: {ex.Message}");
            }
            return friendlies;
        }

        /// <summary>
        /// Get a list of all units (both friendly and enemy) on all floors.
        /// </summary>
        public List<string> GetAllUnits()
        {
            var allUnits = new List<string>();
            allUnits.AddRange(GetAllFriendlyUnits());
            allUnits.AddRange(GetAllEnemies());
            return allUnits;
        }

        /// <summary>
        /// Get pyre health from SaveManager
        /// </summary>
        public int GetPyreHealth()
        {
            if (_cache.SaveManager == null || _cache.GetTowerHPMethod == null)
            {
                _cache.FindManagers();
            }

            try
            {
                var result = _cache.GetTowerHPMethod?.Invoke(_cache.SaveManager, null);
                if (result is int hp) return hp;
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// Get max pyre health from SaveManager
        /// </summary>
        public int GetMaxPyreHealth()
        {
            try
            {
                var result = _cache.GetMaxTowerHPMethod?.Invoke(_cache.SaveManager, null);
                if (result is int hp) return hp;
            }
            catch { }
            return -1;
        }
    }
}
