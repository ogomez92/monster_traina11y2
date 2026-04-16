using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Localized display name for a user-facing floor (1=bottom, 2=middle, 3=top, 0=pyre).
        /// Tries to find a matching I2.Loc term in the game so all languages are covered;
        /// falls back to English if no term is found.
        /// </summary>
        public static string GetFloorDisplayName(int userFloor)
        {
            switch (userFloor)
            {
                case 3: return "Top floor";
                case 2: return "Middle floor";
                case 1: return "Bottom floor";
                case 0: return GetPyreDisplayName();
                default: return $"Floor {userFloor}";
            }
        }

        public static string GetPyreDisplayName() => "Pyre";

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
                for (int userFloor = 3; userFloor >= 1; userFloor--)
                {
                    int roomIndex = userFloor - 1; // Convert user floor to room index (room 0 = bottom)
                    var room = GetRoom(roomIndex);
                    if (room != null)
                    {
                        string floorName = GetFloorDisplayName(userFloor);
                        string capacity = GetMonsterCapacityString(room);
                        string attachments = GetRoomAttachmentsString(room);
                        string corruption = GetRoomCorruptionString(room);
                        var units = UnitInfoHelper.GetUnitsInRoom(room);

                        var parts = new List<string>();
                        if (!string.IsNullOrEmpty(capacity))
                            parts.Add(capacity);
                        if (!string.IsNullOrEmpty(attachments))
                            parts.Add(attachments);
                        if (!string.IsNullOrEmpty(corruption))
                            parts.Add(corruption);

                        if (units.Count == 0)
                        {
                            parts.Add("Empty");
                        }
                        else
                        {
                            var enemyDescriptions = new List<string>();
                            var friendlyDescriptions = new List<string>();
                            foreach (var unit in units)
                            {
                                string desc = FormatUnitForFloorListing(unit);
                                if (UnitInfoHelper.IsEnemyUnit(unit, _cache))
                                    enemyDescriptions.Add(desc);
                                else
                                    friendlyDescriptions.Add(desc);
                            }

                            if (enemyDescriptions.Count > 0)
                                parts.Add($"Enemies: {string.Join(", ", enemyDescriptions)}");
                            if (friendlyDescriptions.Count > 0)
                                parts.Add($"Your units: {string.Join(", ", friendlyDescriptions)}");
                        }

                        output?.Queue($"{floorName}: {string.Join(". ", parts)}");
                    }
                }

                // Announce pyre health
                int pyreHP = GetPyreHealth();
                int maxPyreHP = GetMaxPyreHealth();
                if (pyreHP >= 0)
                {
                    output?.Queue($"{GetPyreDisplayName()}: {pyreHP} of {maxPyreHP} health");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing floors: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read floors", false);
            }
        }

        /// <summary>
        /// Format a single unit for inline floor listings. Includes name, stats, and
        /// equipment if any. Used by AnnounceAllFloors / GetFloorSummary / GetAllEnemies.
        /// </summary>
        private string FormatUnitForFloorListing(object unit)
        {
            string name = UnitInfoHelper.GetUnitName(unit, _cache);
            int hp = UnitInfoHelper.GetUnitHP(unit, _cache);
            int maxHp = UnitInfoHelper.GetUnitMaxHP(unit);
            int attack = UnitInfoHelper.GetUnitAttack(unit, _cache);
            string equipment = UnitInfoHelper.GetUnitEquipment(unit);

            var sb = new StringBuilder();
            sb.Append(name);
            sb.Append(' ');
            sb.Append(attack);
            sb.Append(" attack, ");
            if (maxHp > 0 && hp < maxHp)
                sb.Append($"{hp} of {maxHp} health");
            else
                sb.Append($"{hp} health");

            if (!string.IsNullOrEmpty(equipment))
                sb.Append($", equipped {equipment}");

            var effects = UnitInfoHelper.GetUnitStatusEffectsRaw(unit);
            if (effects.Count > 0)
            {
                var effectStrings = new List<string>();
                foreach (var effect in effects)
                {
                    string announcement = Core.KeywordManager.GetKeywordAnnouncement(effect.Key);
                    effectStrings.Add(effect.Value > 1 ? $"{announcement} {effect.Value}" : announcement);
                }
                sb.Append(". ");
                sb.Append(string.Join("; ", effectStrings));
            }

            return sb.ToString();
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
                    return -1;

                var roomManagerType = _cache.RoomManager.GetType();
                int roomIndex = -1;

                var getSelectedRoomMethod = roomManagerType.GetMethod("GetSelectedRoom", Type.EmptyTypes);
                if (getSelectedRoomMethod != null)
                {
                    var result = getSelectedRoomMethod.Invoke(_cache.RoomManager, null);
                    if (result is int idx)
                        roomIndex = idx;
                }

                // Convert room index to user floor
                if (roomIndex >= 0 && roomIndex <= 3)
                {
                    return roomIndex == 3 ? 0 : roomIndex + 1;
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
                if (_cache.RoomManager == null || _cache.GetRoomMethod == null)
                    return null;
            }

            try
            {
                return _cache.GetRoomMethod?.Invoke(_cache.RoomManager, new object[] { roomIndex });
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
                int roomIndex = floorNumber - 1;

                var room = GetRoom(roomIndex);
                if (room == null)
                {
                    return $"Floor {floorNumber}: Unknown";
                }

                string capacity = GetMonsterCapacityString(room);
                string attachments = GetRoomAttachmentsString(room);
                string corruption = GetRoomCorruptionString(room);
                var units = UnitInfoHelper.GetUnitsInRoom(room);

                var parts = new List<string>();
                if (!string.IsNullOrEmpty(capacity))
                    parts.Add(capacity);
                if (!string.IsNullOrEmpty(attachments))
                    parts.Add(attachments);
                if (!string.IsNullOrEmpty(corruption))
                    parts.Add(corruption);

                if (units.Count == 0)
                {
                    parts.Add("Empty");
                }
                else
                {
                    var friendlyUnits = new List<string>();
                    var enemyUnits = new List<string>();
                    foreach (var unit in units)
                    {
                        string description = FormatUnitForFloorListing(unit);
                        if (UnitInfoHelper.IsEnemyUnit(unit, _cache))
                            enemyUnits.Add(description);
                        else
                            friendlyUnits.Add(description);
                    }
                    if (enemyUnits.Count > 0)
                        parts.Add($"Enemies: {string.Join(", ", enemyUnits)}");
                    if (friendlyUnits.Count > 0)
                        parts.Add($"Your units: {string.Join(", ", friendlyUnits)}");
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
                    int roomIndex = floorNumber - 1;
                    var room = GetRoom(roomIndex);
                    if (room == null) continue;

                    var units = UnitInfoHelper.GetUnitsInRoom(room);
                    foreach (var unit in units)
                    {
                        if (UnitInfoHelper.IsEnemyUnit(unit, _cache))
                        {
                            enemies.Add($"{FormatUnitForFloorListing(unit)} on floor {floorNumber}");
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
                    int roomIndex = floorNumber - 1;
                    var room = GetRoom(roomIndex);
                    if (room == null) continue;

                    var units = UnitInfoHelper.GetUnitsInRoom(room);
                    foreach (var unit in units)
                    {
                        if (!UnitInfoHelper.IsEnemyUnit(unit, _cache))
                        {
                            friendlies.Add($"{FormatUnitForFloorListing(unit)} on floor {floorNumber}");
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
            allUnits.AddRange(GetAllEnemies());
            allUnits.AddRange(GetAllFriendlyUnits());
            return allUnits;
        }

        // Cached reflection bits for RoomState corruption.
        private static System.Reflection.FieldInfo _corruptionEnabledField;
        private static System.Reflection.MethodInfo _getCurrentCorruptionMethod;
        private static System.Reflection.MethodInfo _getMaxCorruptionMethod;

        /// <summary>
        /// Report the room's corruption state as "Corrupted X of Y" when corruption is
        /// enabled on that floor. Returns null when corruption isn't active so the
        /// announcement stays quiet on floors that don't care.
        /// </summary>
        private string GetRoomCorruptionString(object room)
        {
            if (room == null) return null;
            try
            {
                var roomType = room.GetType();
                if (_getCurrentCorruptionMethod == null || _getCurrentCorruptionMethod.DeclaringType != roomType)
                {
                    _corruptionEnabledField = roomType.GetField("corruptionEnabled",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    // Prefer GetCurrentNonPreviewCorruption — the preview variant can
                    // return stale/zero values when not actively previewing a card play.
                    _getCurrentCorruptionMethod = roomType.GetMethod("GetCurrentNonPreviewCorruption", Type.EmptyTypes)
                        ?? roomType.GetMethod("GetCurrentCorruption", Type.EmptyTypes);
                    _getMaxCorruptionMethod = roomType.GetMethod("GetMaxCorruption", Type.EmptyTypes);
                }

                int current = 0, max = 0;
                if (_getCurrentCorruptionMethod != null)
                {
                    var r = _getCurrentCorruptionMethod.Invoke(room, null);
                    if (r is int c) current = c;
                }
                if (_getMaxCorruptionMethod != null)
                {
                    var r = _getMaxCorruptionMethod.Invoke(room, null);
                    if (r is int m) max = m;
                }

                bool enabled = false;
                if (_corruptionEnabledField != null)
                {
                    var enabledObj = _corruptionEnabledField.GetValue(room);
                    if (enabledObj is bool b) enabled = b;
                }

                // Skip only if this floor has no corruption concept and no current
                // corruption. Always surface current corruption when > 0 so the
                // player can tell they're on a corrupted floor.
                if (!enabled && current <= 0) return null;

                if (max <= 0) return current > 0 ? $"Corrupted {current}" : "Corruption enabled";
                return $"Corrupted {current} of {max}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetRoomCorruptionString error: {ex}");
                return null;
            }
        }

        // Cached reflection bits for RoomState.Attachments -> TrainRoomAttachmentState.
        private static System.Reflection.PropertyInfo _attachmentsProp;
        private static System.Reflection.PropertyInfo _roomStateModifiersProp;
        private static System.Reflection.MethodInfo _getDescriptionKeyInPlay;
        private static System.Reflection.MethodInfo _getDescriptionKey;

        /// <summary>
        /// Build a comma-separated list of active TrainRoomAttachments on the given room,
        /// localizing each attachment's IRoomStateModifier description. Used to surface
        /// floor-state like "Pyre Dampener", "Phoenix Pyre", etc. during floor browsing.
        /// </summary>
        private string GetRoomAttachmentsString(object room)
        {
            if (room == null) return null;
            try
            {
                var roomType = room.GetType();
                if (_attachmentsProp == null || _attachmentsProp.DeclaringType != roomType)
                {
                    _attachmentsProp = roomType.GetProperty("Attachments", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (_attachmentsProp == null)
                    {
                        MonsterTrainAccessibility.LogWarning($"GetRoomAttachmentsString: Attachments property not found on {roomType.FullName}");
                        return null;
                    }
                }

                var attachments = _attachmentsProp.GetValue(room) as System.Collections.IEnumerable;
                if (attachments == null) return null;

                var descriptions = new List<string>();
                foreach (var attachment in attachments)
                {
                    if (attachment == null) continue;
                    try
                    {
                        var attType = attachment.GetType();
                        var isActiveProp = attType.GetProperty("IsActive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (isActiveProp != null)
                        {
                            var active = isActiveProp.GetValue(attachment);
                            if (active is bool b && !b) continue;
                        }

                        if (_roomStateModifiersProp == null || _roomStateModifiersProp.DeclaringType != attType)
                        {
                            _roomStateModifiersProp = attType.GetProperty("RoomStateModifiers", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        }
                        var modifiers = _roomStateModifiersProp?.GetValue(attachment) as System.Collections.IEnumerable;
                        if (modifiers == null) continue;

                        foreach (var mod in modifiers)
                        {
                            if (mod == null) continue;
                            string desc = ResolveModifierDescription(mod);
                            if (!string.IsNullOrEmpty(desc) && !descriptions.Contains(desc))
                                descriptions.Add(desc);
                        }
                    }
                    catch (Exception ex)
                    {
                        MonsterTrainAccessibility.LogError($"GetRoomAttachmentsString attachment error: {ex}");
                    }
                }

                if (descriptions.Count == 0) return null;
                return string.Join(". ", descriptions);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetRoomAttachmentsString error: {ex}");
                return null;
            }
        }

        private string ResolveModifierDescription(object modifier)
        {
            try
            {
                var modType = modifier.GetType();
                if (_getDescriptionKeyInPlay == null || _getDescriptionKeyInPlay.DeclaringType != modType)
                {
                    _getDescriptionKeyInPlay = modType.GetMethod("GetDescriptionKeyInPlay", Type.EmptyTypes);
                    _getDescriptionKey = modType.GetMethod("GetDescriptionKey", Type.EmptyTypes);
                }

                string key = _getDescriptionKeyInPlay?.Invoke(modifier, null) as string;
                if (string.IsNullOrEmpty(key))
                    key = _getDescriptionKey?.Invoke(modifier, null) as string;
                if (string.IsNullOrEmpty(key)) return null;

                string localized = Utilities.LocalizationHelper.Localize(key);
                if (string.IsNullOrEmpty(localized)) return null;

                localized = Utilities.TextUtilities.CleanSpriteTagsForSpeech(localized);
                localized = Utilities.TextUtilities.StripRichTextTags(localized);
                return localized?.Trim();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ResolveModifierDescription error: {ex}");
                return null;
            }
        }

        // Cached reflection bits for RoomState.GetCapacityInfo(Team.Type team).
        // The struct CapacityInfo exposes `count` (used) and `max` (total).
        private static System.Reflection.MethodInfo _getCapacityInfoMethod;
        private static System.Reflection.FieldInfo _capacityCountField;
        private static System.Reflection.FieldInfo _capacityMaxField;
        private static object _teamTypeMonsters;

        /// <summary>
        /// Format the monster-team capacity for the given room as "Capacity X of Y".
        /// Returns null only when an actual error happens — and logs loudly so we can
        /// diagnose later. No silent kill switch.
        /// </summary>
        private string GetMonsterCapacityString(object room)
        {
            if (room == null) return null;
            try
            {
                var roomType = room.GetType();
                if (_getCapacityInfoMethod == null || _getCapacityInfoMethod.DeclaringType != roomType)
                {
                    _getCapacityInfoMethod = roomType.GetMethod("GetCapacityInfo");
                    if (_getCapacityInfoMethod == null)
                    {
                        MonsterTrainAccessibility.LogWarning($"GetMonsterCapacityString: GetCapacityInfo method not found on {roomType.FullName}");
                        return null;
                    }
                }

                if (_teamTypeMonsters == null)
                {
                    var teamParamType = _getCapacityInfoMethod.GetParameters()[0].ParameterType;
                    if (!teamParamType.IsEnum)
                    {
                        MonsterTrainAccessibility.LogWarning($"GetMonsterCapacityString: GetCapacityInfo param is not an enum, got {teamParamType.FullName}");
                        return null;
                    }
                    _teamTypeMonsters = Enum.Parse(teamParamType, "Monsters");
                }

                var info = _getCapacityInfoMethod.Invoke(room, new[] { _teamTypeMonsters });
                if (info == null)
                {
                    MonsterTrainAccessibility.LogWarning("GetMonsterCapacityString: GetCapacityInfo returned null");
                    return null;
                }

                if (_capacityCountField == null || _capacityMaxField == null)
                {
                    var infoType = info.GetType();
                    _capacityCountField = infoType.GetField("count");
                    _capacityMaxField = infoType.GetField("max");
                    if (_capacityCountField == null || _capacityMaxField == null)
                    {
                        MonsterTrainAccessibility.LogWarning($"GetMonsterCapacityString: count/max fields missing on {infoType.FullName} (fields: {string.Join(", ", infoType.GetFields().Select(f => f.Name))})");
                        return null;
                    }
                }

                int used = Convert.ToInt32(_capacityCountField.GetValue(info));
                int max = Convert.ToInt32(_capacityMaxField.GetValue(info));
                if (max <= 0)
                {
                    MonsterTrainAccessibility.LogWarning($"GetMonsterCapacityString: nonsensical max={max}, used={used}");
                    return null;
                }
                return $"Capacity {used} of {max}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetMonsterCapacityString error: {ex}");
                return null;
            }
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
