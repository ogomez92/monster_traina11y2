using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect when a new wave of enemies starts spawning.
    /// Hooks into the wave/spawn wave system to announce new enemy waves.
    /// </summary>
    public static class WaveStartPatch
    {
        private static int _lastWaveAnnounced = -1;
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try CombatManager.SpawnWave or similar
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType != null)
                {
                    MethodInfo method = null;
                    var candidates = new[] {
                        "SpawnWave", "StartWave", "SpawnEnemyWave", "SpawnHeroWave",
                        "BeginWave", "ProcessWave", "StartNextWave"
                    };

                    foreach (var name in candidates)
                    {
                        method = AccessTools.Method(combatType, name);
                        if (method != null) break;
                    }

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(WaveStartPatch).GetMethod(nameof(CombatManagerPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched CombatManager.{method.Name} for wave announcements");
                        return;
                    }
                }

                // Try SpawnManager
                var spawnType = AccessTools.TypeByName("SpawnManager");
                if (spawnType != null)
                {
                    MethodInfo method = null;
                    var candidates = new[] {
                        "SpawnWave", "StartWave", "SpawnNextWave",
                        "ProcessSpawnWave", "DoSpawnWave"
                    };

                    foreach (var name in candidates)
                    {
                        method = AccessTools.Method(spawnType, name);
                        if (method != null) break;
                    }

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(WaveStartPatch).GetMethod(nameof(SpawnManagerPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched SpawnManager.{method.Name} for wave announcements");
                        return;
                    }
                }

                MonsterTrainAccessibility.LogInfo("Wave spawn methods not found - wave announcements disabled");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping wave start patch: {ex.Message}");
            }
        }

        public static void CombatManagerPostfix(object __instance)
        {
            AnnounceWave(__instance);
        }

        public static void SpawnManagerPostfix(object __instance)
        {
            AnnounceWave(__instance);
        }

        private static void AnnounceWave(object manager)
        {
            try
            {
                float currentTime = UnityEngine.Time.unscaledTime;
                if (currentTime - _lastAnnouncedTime < 1.0f)
                    return;
                _lastAnnouncedTime = currentTime;

                // Try to get wave number
                int waveNumber = GetWaveNumber(manager);

                if (waveNumber > 0 && waveNumber != _lastWaveAnnounced)
                {
                    _lastWaveAnnounced = waveNumber;
                    MonsterTrainAccessibility.BattleHandler?.OnWaveStarted(waveNumber);
                }
                else if (waveNumber <= 0)
                {
                    // Can't determine wave number, just announce generically
                    MonsterTrainAccessibility.BattleHandler?.OnWaveStarted(-1);
                }
            }
            catch { }
        }

        private static int GetWaveNumber(object manager)
        {
            try
            {
                var type = manager.GetType();

                // Try common wave tracking fields/properties
                var candidates = new[] { "CurrentWave", "WaveIndex", "currentWave", "waveIndex", "CurrentWaveIndex" };
                foreach (var name in candidates)
                {
                    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
                    {
                        var val = prop.GetValue(manager);
                        if (val is int i) return i + 1; // Convert 0-based to 1-based
                    }

                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var val = field.GetValue(manager);
                        if (val is int i) return i + 1;
                    }
                }
            }
            catch { }
            return -1;
        }
    }
}
