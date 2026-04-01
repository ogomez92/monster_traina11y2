using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect pyre damage
    /// </summary>
    public static class PyreDamagePatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try to find the pyre/tower damage method
                var saveManagerType = AccessTools.TypeByName("SaveManager");
                if (saveManagerType != null)
                {
                    var method = AccessTools.Method(saveManagerType, "SetTowerHP") ??
                                 AccessTools.Method(saveManagerType, "DamageTower") ??
                                 AccessTools.Method(saveManagerType, "ModifyTowerHP");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(PyreDamagePatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched pyre damage: {method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch pyre damage: {ex.Message}");
            }
        }

        private static int _lastPyreHP = -1;

        public static void Postfix(object __instance)
        {
            try
            {
                // Get current pyre HP
                var type = __instance.GetType();
                var getHPMethod = type.GetMethod("GetTowerHP");
                if (getHPMethod != null)
                {
                    var result = getHPMethod.Invoke(__instance, null);
                    if (result is int currentHP)
                    {
                        if (_lastPyreHP > 0 && currentHP < _lastPyreHP)
                        {
                            int damage = _lastPyreHP - currentHP;
                            MonsterTrainAccessibility.BattleHandler?.OnPyreDamaged(damage, currentHP);
                        }
                        _lastPyreHP = currentHP;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in pyre damage patch: {ex.Message}");
            }
        }
    }
}
