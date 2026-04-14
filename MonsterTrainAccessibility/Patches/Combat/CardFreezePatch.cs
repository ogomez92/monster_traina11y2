using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when an enemy (or any effect) freezes cards in the player's hand.
    /// Hooks the three relevant card effects: FreezeAllCards, FreezeRandomCard,
    /// FreezeCard — each adds CardTraitFreeze to one or more cards, keeping them
    /// stuck in hand. Without this, the player hears nothing and silently loses
    /// card economy.
    /// </summary>
    public static class CardFreezePatch
    {
        private static string _lastAnnounced = "";
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            PatchEffect(harmony, "CardEffectFreezeAllCards", "All cards in hand frozen");
            PatchEffect(harmony, "CardEffectFreezeRandomCard", "Random card in hand frozen");
            PatchEffect(harmony, "CardEffectFreezeCard", "Card in hand frozen");
        }

        private static void PatchEffect(Harmony harmony, string typeName, string message)
        {
            try
            {
                var effectType = AccessTools.TypeByName(typeName);
                if (effectType == null)
                {
                    MonsterTrainAccessibility.LogWarning($"CardFreezePatch: {typeName} not found");
                    return;
                }

                var method = AccessTools.Method(effectType, "ApplyEffect");
                if (method == null)
                {
                    MonsterTrainAccessibility.LogWarning($"CardFreezePatch: {typeName}.ApplyEffect not found");
                    return;
                }

                var postfix = new HarmonyMethod(typeof(CardFreezePatch).GetMethod(nameof(AnnouncePostfix)));
                postfix.priority = Priority.Normal;
                harmony.Patch(method, postfix: postfix);
                _messagesByType[typeName] = message;
                MonsterTrainAccessibility.LogInfo($"Patched {typeName}.ApplyEffect for freeze announcements");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"CardFreezePatch.PatchEffect({typeName}) failed: {ex}");
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> _messagesByType =
            new System.Collections.Generic.Dictionary<string, string>();

        public static void AnnouncePostfix(MethodBase __originalMethod)
        {
            try
            {
                string typeName = __originalMethod?.DeclaringType?.Name;
                if (string.IsNullOrEmpty(typeName)) return;
                if (!_messagesByType.TryGetValue(typeName, out string message)) return;

                float now = UnityEngine.Time.unscaledTime;
                if (typeName == _lastAnnounced && now - _lastAnnouncedTime < 0.5f) return;
                _lastAnnounced = typeName;
                _lastAnnouncedTime = now;

                MonsterTrainAccessibility.ScreenReader?.Queue(message);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"CardFreezePatch.AnnouncePostfix error: {ex}");
            }
        }
    }
}
