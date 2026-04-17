using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce pyre armor changes by hooking the method that dispatches
    /// CombatManager.pyreArmorChangedSignal. We patch the SetPyreArmor or
    /// ModifyPyreArmor method on CombatManager (or SaveManager).
    /// </summary>
    public static class PyreArmorPatch
    {
        private static int _lastArmor = -1;
        private static float _lastTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try CombatManager first for armor methods
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType != null)
                {
                    MethodInfo method = null;
                    var candidates = new[] { "SetPyreArmor", "ModifyPyreArmor", "AddPyreArmor", "SetTowerArmor", "ModifyTowerArmor" };
                    foreach (var name in candidates)
                    {
                        method = AccessTools.Method(combatType, name);
                        if (method != null) break;
                    }

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(PyreArmorPatch).GetMethod(nameof(CombatManagerPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched CombatManager.{method.Name} for pyre armor");
                        return;
                    }
                }

                // Try SaveManager as fallback
                var saveType = AccessTools.TypeByName("SaveManager");
                if (saveType != null)
                {
                    MethodInfo method = null;
                    var candidates = new[] { "SetTowerArmor", "ModifyTowerArmor", "SetPyreArmor" };
                    foreach (var name in candidates)
                    {
                        method = AccessTools.Method(saveType, name);
                        if (method != null) break;
                    }

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(PyreArmorPatch).GetMethod(nameof(SaveManagerPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched SaveManager.{method.Name} for pyre armor");
                        return;
                    }
                }

                MonsterTrainAccessibility.LogInfo("Pyre armor methods not found - armor announcements disabled");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping pyre armor patch: {ex.Message}");
            }
        }

        public static void CombatManagerPostfix(object __instance, int __0)
        {
            AnnounceArmorChange(__0);
        }

        public static void SaveManagerPostfix(object __instance, int __0)
        {
            AnnounceArmorChange(__0);
        }

        private static void AnnounceArmorChange(int armorValue)
        {
            try
            {
                float now = UnityEngine.Time.unscaledTime;
                if (armorValue == _lastArmor && now - _lastTime < 0.3f) return;
                _lastArmor = armorValue;
                _lastTime = now;

                MonsterTrainAccessibility.BattleHandler?.OnPyreArmorChanged(armorValue);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in pyre armor patch: {ex.Message}");
            }
        }
    }
}
