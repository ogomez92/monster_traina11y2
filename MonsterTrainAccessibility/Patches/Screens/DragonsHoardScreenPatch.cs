using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for Dragon's Hoard screen (MT2 specific)
    /// </summary>
    public static class DragonsHoardScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("DragonsHoardScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("DragonsHoardScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(DragonsHoardScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched DragonsHoardScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("DragonsHoardScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DragonsHoardScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Dragon's Hoard screen entered");
                ScreenStateTracker.SetScreen(Help.GameScreen.DragonsHoard);

                var (amount, cap) = GetHoardCounts(__instance);
                string hoardName = Utilities.ModLocalization.DragonsHoard;
                string countText = cap > 0
                    ? $" {amount}/{cap} {hoardName} stored."
                    : amount > 0 ? $" {amount} {hoardName} stored." : "";

                MonsterTrainAccessibility.ScreenReader?.Speak($"{hoardName}.{countText} Use arrow keys to browse options. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DragonsHoardScreen patch: {ex.Message}");
            }
        }

        private static (int amount, int cap) GetHoardCounts(object screen)
        {
            object saveManager = null;
            try
            {
                if (screen != null)
                {
                    var field = screen.GetType().GetField("saveManager",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    saveManager = field?.GetValue(screen);
                }
            }
            catch { }

            if (saveManager == null)
            {
                saveManager = Utilities.ReflectionHelper.FindManager("SaveManager");
            }

            if (saveManager == null) return (0, 0);

            int amount = 0, cap = 0;
            try
            {
                var t = saveManager.GetType();
                var getAmt = t.GetMethod("GetDragonsHoardAmount", Type.EmptyTypes);
                var getCap = t.GetMethod("GetDragonsHoardCap", Type.EmptyTypes);
                if (getAmt?.Invoke(saveManager, null) is int a) amount = a;
                if (getCap?.Invoke(saveManager, null) is int c) cap = c;
            }
            catch { }
            return (amount, cap);
        }
    }
}
