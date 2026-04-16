using HarmonyLib;
using MonsterTrainAccessibility.Battle;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce floor changes when the player browses floors with PgUp/PgDn during
    /// card selection. The previous EventSystem-based heuristic doesn't catch this
    /// because MT2 calls RoomManager.RoomSelectionChanged directly without moving
    /// the EventSystem selection — focus stays on the card. Hooking the room
    /// manager method works regardless of UI focus.
    /// </summary>
    public static class RoomSelectionPatch
    {
        private static int _lastAnnouncedRoom = -1;
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var rmType = AccessTools.TypeByName("RoomManager");
                if (rmType == null)
                {
                    MonsterTrainAccessibility.LogWarning("RoomSelectionPatch: RoomManager type not found");
                    return;
                }

                var method = AccessTools.Method(rmType, "RoomSelectionChanged");
                if (method == null)
                {
                    MonsterTrainAccessibility.LogWarning("RoomSelectionPatch: RoomSelectionChanged method not found");
                    return;
                }

                var postfix = new HarmonyMethod(typeof(RoomSelectionPatch).GetMethod(nameof(Postfix)));
                harmony.Patch(method, postfix: postfix);
                MonsterTrainAccessibility.LogInfo("Patched RoomManager.RoomSelectionChanged");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"RoomSelectionPatch.TryPatch failed: {ex}");
            }
        }

        // RoomSelectionChanged(int prevRoom, int newRoom, RoomState.SelectionMode mode)
        public static void Postfix(int __1, object __instance)
        {
            try
            {
                int newRoom = __1;
                if (newRoom < 0 || newRoom > 3) return;

                // Only announce when the player actually pressed Page Up/Down.
                // The game re-fires RoomSelectionChanged for lots of internal reasons
                // (card plays, floor state changes, animations) which spams the reader.
                if (!UnityEngine.Input.GetKey(UnityEngine.KeyCode.PageUp) &&
                    !UnityEngine.Input.GetKey(UnityEngine.KeyCode.PageDown))
                    return;

                // FloorTargetingSystem already handles announcements during card targeting.
                if (FloorTargetingSystem.Instance != null && FloorTargetingSystem.Instance.IsTargeting)
                    return;

                // Debounce: same room within 0.15s = ignore (the game often re-fires).
                float now = UnityEngine.Time.unscaledTime;
                if (newRoom == _lastAnnouncedRoom && now - _lastAnnouncedTime < 0.15f) return;
                _lastAnnouncedRoom = newRoom;
                _lastAnnouncedTime = now;

                var battle = MonsterTrainAccessibility.BattleHandler;
                if (battle == null || !battle.IsInBattle) return;

                // Convert internal room index → user floor (room 3 is pyre, 0=top, 1=mid, 2=bot)
                int userFloor = newRoom == 3 ? 0 : newRoom + 1;
                string floorName = FloorReader.GetFloorDisplayName(userFloor);
                string summary = battle.GetFloorSummary(userFloor);

                string message = string.IsNullOrEmpty(summary) ? floorName : $"{floorName}. {summary}";
                MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"RoomSelectionPatch.Postfix error: {ex}");
            }
        }
    }
}
