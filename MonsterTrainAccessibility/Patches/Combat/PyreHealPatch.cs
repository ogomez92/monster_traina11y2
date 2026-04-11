using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect when the pyre is healed. Connects to the existing
    /// BattleAccessibility.OnPyreHealed() method that was previously unused.
    /// </summary>
    public static class PyreHealPatch
    {
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try SaveManager.HealTower or similar
                var saveManagerType = AccessTools.TypeByName("SaveManager");
                if (saveManagerType != null)
                {
                    MethodInfo method = null;
                    var candidates = new[] { "HealTower", "AddTowerHP", "RestoreTowerHP", "ModifyTowerHP" };

                    foreach (var name in candidates)
                    {
                        method = AccessTools.Method(saveManagerType, name);
                        if (method != null) break;
                    }

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(PyreHealPatch).GetMethod(nameof(SaveManagerPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched SaveManager.{method.Name} for pyre heal announcements");
                        return;
                    }
                }

                // Try CharacterState on the pyre character
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType != null)
                {
                    var healMethod = AccessTools.Method(characterType, "ApplyHeal");
                    // ApplyHeal is already patched by HealAppliedPatch, but that only announces unit heals.
                    // The pyre uses SaveManager, not CharacterState.ApplyHeal typically.
                }

                MonsterTrainAccessibility.LogInfo("Pyre heal methods not found - pyre heal announcements disabled");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping pyre heal patch: {ex.Message}");
            }
        }

        public static void SaveManagerPostfix(object __instance, int __0)
        {
            try
            {
                if (__0 <= 0) return;

                float currentTime = UnityEngine.Time.unscaledTime;
                if (currentTime - _lastAnnouncedTime < 0.3f)
                    return;
                _lastAnnouncedTime = currentTime;

                // Get current pyre HP after healing
                int currentHP = GetCurrentPyreHP(__instance);
                MonsterTrainAccessibility.BattleHandler?.OnPyreHealed(__0, currentHP);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in pyre heal patch: {ex.Message}");
            }
        }

        private static int GetCurrentPyreHP(object saveManager)
        {
            try
            {
                var type = saveManager.GetType();
                var method = type.GetMethod("GetTowerHP") ?? type.GetMethod("GetCurrentTowerHP");
                if (method != null)
                {
                    var result = method.Invoke(saveManager, null);
                    if (result is int hp) return hp;
                }
            }
            catch { }
            return -1;
        }
    }
}
