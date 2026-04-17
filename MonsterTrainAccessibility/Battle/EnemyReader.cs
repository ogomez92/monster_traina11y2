using System;
using System.Collections.Generic;

namespace MonsterTrainAccessibility.Battle
{
    /// <summary>
    /// Reads and announces enemy/unit information from all floors.
    /// </summary>
    public class EnemyReader
    {
        private readonly BattleManagerCache _cache;
        private readonly FloorReader _floorReader;

        public EnemyReader(BattleManagerCache cache, FloorReader floorReader)
        {
            _cache = cache;
            _floorReader = floorReader;
        }

        /// <summary>
        /// Announce all units (player monsters and enemies) on each floor
        /// </summary>
        public void AnnounceEnemies()
        {
            try
            {
                var output = MonsterTrainAccessibility.ScreenReader;
                output?.Speak("", false);

                bool hasAnyUnits = false;
                int roomsFound = 0;
                int totalUnits = 0;

                // Iterate user floors from bottom (1) to top (3), then pyre room
                // Room indices: 0=bottom, 1=middle, 2=top, 3=pyre room
                // User floors: 1=bottom, 2=middle, 3=top
                int[] userFloors = { 1, 2, 3 };

                foreach (int userFloor in userFloors)
                {
                    int roomIndex = userFloor - 1; // Convert user floor to room index
                    var room = _floorReader.GetRoom(roomIndex);
                    if (room == null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Room {roomIndex} (floor {userFloor}) is null");
                        continue;
                    }
                    roomsFound++;

                    var units = UnitInfoHelper.GetUnitsInRoom(room);
                    totalUnits += units.Count;
                    MonsterTrainAccessibility.LogInfo($"Room {roomIndex} (floor {userFloor}) has {units.Count} units");

                    string floorName = Utilities.ModLocalization.FloorName(userFloor);
                    var playerDescriptions = new List<string>();
                    var enemyDescriptions = new List<string>();

                    foreach (var unit in units)
                    {
                        bool isEnemy = UnitInfoHelper.IsEnemyUnit(unit, _cache);
                        string unitDesc = UnitInfoHelper.GetDetailedUnitDescription(unit, _cache);

                        if (isEnemy)
                        {
                            enemyDescriptions.Add(unitDesc);
                        }
                        else
                        {
                            playerDescriptions.Add(unitDesc);
                        }
                    }

                    // Announce floor if it has any units
                    if (playerDescriptions.Count > 0 || enemyDescriptions.Count > 0)
                    {
                        hasAnyUnits = true;
                        output?.Queue($"{floorName}:");

                        foreach (var desc in playerDescriptions)
                        {
                            output?.Queue($"  {desc}");
                        }

                        foreach (var desc in enemyDescriptions)
                        {
                            output?.Queue($"  {desc}");
                        }
                    }
                }

                // Also check pyre room (room index 3)
                var pyreRoom = _floorReader.GetRoom(3);
                if (pyreRoom != null)
                {
                    roomsFound++;
                    var pyreUnits = UnitInfoHelper.GetUnitsInRoom(pyreRoom);
                    totalUnits += pyreUnits.Count;
                    // Pyre room units would be announced here if needed, but typically empty
                }

                MonsterTrainAccessibility.LogInfo($"AnnounceEnemies: found {roomsFound} rooms, {totalUnits} total units, hasAnyUnits: {hasAnyUnits}");

                if (!hasAnyUnits)
                {
                    output?.Queue("0");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing units: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read units", false);
            }
        }
    }
}
