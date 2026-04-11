using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect when trigger abilities (Slay, Incant, Revenge, etc.) fire on units.
    /// Hooks into CharacterTriggerData or related trigger processing methods.
    /// </summary>
    public static class TriggerAbilityPatch
    {
        private static string _lastAnnouncedTrigger = "";
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try to patch the trigger firing method on CharacterState
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType != null)
                {
                    // Look for trigger execution methods
                    MethodInfo method = null;
                    var candidates = new[] {
                        "FireTrigger", "ExecuteTrigger", "OnTriggered",
                        "ProcessTrigger", "ActivateTrigger"
                    };

                    foreach (var name in candidates)
                    {
                        method = AccessTools.Method(characterType, name);
                        if (method != null) break;
                    }

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(TriggerAbilityPatch).GetMethod(nameof(CharacterTriggerPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched CharacterState.{method.Name} for trigger announcements");
                        return;
                    }
                }

                // Try CharacterTriggerData approach
                var triggerDataType = AccessTools.TypeByName("CharacterTriggerData");
                if (triggerDataType != null)
                {
                    MethodInfo method = null;
                    var candidates = new[] {
                        "FireTrigger", "Execute", "ActivateTrigger",
                        "ProcessTrigger", "Fire"
                    };

                    foreach (var name in candidates)
                    {
                        var methods = triggerDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var m in methods)
                        {
                            if (m.Name == name)
                            {
                                method = m;
                                break;
                            }
                        }
                        if (method != null) break;
                    }

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(TriggerAbilityPatch).GetMethod(nameof(TriggerDataPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched CharacterTriggerData.{method.Name} for trigger announcements");
                        return;
                    }
                }

                MonsterTrainAccessibility.LogInfo("Trigger ability methods not found - trigger announcements disabled");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping trigger ability patch: {ex.Message}");
            }
        }

        public static void CharacterTriggerPostfix(object __instance, object[] __args)
        {
            try
            {
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);
                string triggerName = __args != null && __args.Length > 0
                    ? CleanTriggerName(__args[0]?.ToString())
                    : "ability";

                if (string.IsNullOrEmpty(triggerName) || triggerName == "None")
                    return;

                AnnounceTrigger(unitName, triggerName);
            }
            catch { }
        }

        public static void TriggerDataPostfix(object __instance, object[] __args)
        {
            try
            {
                // Get the trigger type from the CharacterTriggerData
                string triggerName = null;

                var triggerType = __instance.GetType();
                var getTriggerMethod = triggerType.GetMethod("GetTrigger") ??
                                       triggerType.GetProperty("Trigger")?.GetGetMethod();
                if (getTriggerMethod != null)
                {
                    var trigger = getTriggerMethod.Invoke(__instance, null);
                    if (trigger != null)
                        triggerName = CleanTriggerName(trigger.ToString());
                }

                if (string.IsNullOrEmpty(triggerName) || triggerName == "None")
                    return;

                // Try to get the owning character name
                string unitName = "Unit";
                if (__args != null && __args.Length > 0)
                {
                    unitName = CharacterStateHelper.GetUnitName(__args[0]) ?? "Unit";
                }

                AnnounceTrigger(unitName, triggerName);
            }
            catch { }
        }

        private static void AnnounceTrigger(string unitName, string triggerName)
        {
            // Deduplication
            string key = $"{unitName}_{triggerName}";
            float currentTime = UnityEngine.Time.unscaledTime;
            if (key == _lastAnnouncedTrigger && currentTime - _lastAnnouncedTime < 0.5f)
                return;

            _lastAnnouncedTrigger = key;
            _lastAnnouncedTime = currentTime;

            MonsterTrainAccessibility.BattleHandler?.OnTriggerAbilityFired(unitName, triggerName);
        }

        private static string CleanTriggerName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;

            // Convert enum-style names to readable text
            string name = raw
                .Replace("OnDeath", "Extinguish")
                .Replace("OnKill", "Slay")
                .Replace("OnHit", "Strike")
                .Replace("OnDamaged", "Revenge")
                .Replace("OnSpellCast", "Incant")
                .Replace("OnUnitPlayed", "Rally")
                .Replace("OnAnyUnitDeath", "Harvest")
                .Replace("PostCombat", "Resolve")
                .Replace("OnHealed", "Rejuvenate")
                .Replace("OnEaten", "Gorge")
                .Replace("PreCombat", "Action")
                .Replace("OnHatched", "Hatch")
                .Replace("OnEchoGained", "Inspire")
                .Replace("OnSummoned", "Summon");

            // Add space before capital letters
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name.Trim();
        }
    }
}
