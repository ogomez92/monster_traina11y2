using HarmonyLib;
using System;
using System.Reflection;
using MonsterTrainAccessibility.Patches;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when a character receives or loses equipment.
    /// Hooks CharacterState.AddEquipment(CardState, ICoreGameManagers, bool grafted) and
    /// CharacterState.RemoveEquipment(ICoreGameManagers, CardState).
    /// Both are IEnumerator coroutines - Harmony patches the outer method, which runs when
    /// the coroutine is first invoked (before the state machine starts yielding).
    /// </summary>
    public static class EquipmentPatch
    {
        private static string _lastKey = "";
        private static float _lastTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                MethodInfo addMethod = null;
                MethodInfo removeMethod = null;

                foreach (var m in characterType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name == "AddEquipment" && addMethod == null)
                        addMethod = m;
                    else if (m.Name == "RemoveEquipment" && removeMethod == null)
                        removeMethod = m;
                }

                if (addMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(EquipmentPatch).GetMethod(nameof(AddPrefix)));
                    harmony.Patch(addMethod, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.AddEquipment");
                }

                if (removeMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(EquipmentPatch).GetMethod(nameof(RemovePrefix)));
                    harmony.Patch(removeMethod, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.RemoveEquipment");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch equipment methods: {ex.Message}");
            }
        }

        // AddEquipment(CardState equipmentCard, ICoreGameManagers coreGameManagers, bool grafted)
        public static void AddPrefix(object __instance, object __0)
        {
            try
            {
                if (__instance == null || __0 == null) return;
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance)) return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);
                string equipmentName = GetCardTitle(__0);
                if (string.IsNullOrEmpty(equipmentName)) return;

                if (!Dedupe($"add_{unitName}_{equipmentName}")) return;

                MonsterTrainAccessibility.BattleHandler?.OnEquipmentAdded(unitName, equipmentName);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in AddEquipment patch: {ex.Message}");
            }
        }

        // RemoveEquipment(ICoreGameManagers coreGameManagers, CardState equipmentCard)
        public static void RemovePrefix(object __instance, object __1)
        {
            try
            {
                if (__instance == null || __1 == null) return;
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance)) return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);
                string equipmentName = GetCardTitle(__1);
                if (string.IsNullOrEmpty(equipmentName)) return;

                if (!Dedupe($"remove_{unitName}_{equipmentName}")) return;

                MonsterTrainAccessibility.BattleHandler?.OnEquipmentRemoved(unitName, equipmentName);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RemoveEquipment patch: {ex.Message}");
            }
        }

        private static string GetCardTitle(object cardState)
        {
            try
            {
                var t = cardState.GetType();
                var getTitle = t.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitle != null)
                {
                    var title = getTitle.Invoke(cardState, null) as string;
                    if (!string.IsNullOrEmpty(title))
                        return Utilities.TextUtilities.StripRichTextTags(title);
                }
            }
            catch { }
            return null;
        }

        private static bool Dedupe(string key)
        {
            float now = UnityEngine.Time.unscaledTime;
            if (key == _lastKey && now - _lastTime < 0.3f) return false;
            _lastKey = key;
            _lastTime = now;
            return true;
        }
    }
}
