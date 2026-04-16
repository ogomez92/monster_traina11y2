namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for deck viewing screen.
    /// Detected via screen name check since DeckScreen doesn't have a dedicated GameScreen enum value.
    /// </summary>
    public class DeckViewHelp : IHelpContext
    {
        public string ContextId => "deck_view";
        public string ContextName => "Deck View";
        public int Priority => 65; // Between map (60) and shop (70)

        // Track activation since DeckScreen doesn't have a GameScreen enum value
        private static bool _isActive = false;
        private static float _lastActivationTime = 0f;

        public static void SetActive(bool active)
        {
            _isActive = active;
            if (active)
            {
                _lastActivationTime = UnityEngine.Time.unscaledTime;
            }
            MonsterTrainAccessibility.LogInfo($"DeckViewHelp.SetActive({active})");
        }

        public bool IsActive()
        {
            // Auto-deactivate after 60 seconds as safety fallback
            if (_isActive && UnityEngine.Time.unscaledTime - _lastActivationTime > 60f)
            {
                _isActive = false;
            }
            return _isActive;
        }

        public string GetHelpText()
        {
            return "Arrow keys: Browse cards in your deck. " +
                   "Enter or Space: View card details. " +
                   "F5: Re-read current card. " +
                   "F6: Read all cards (may be long). " +
                   "Tab: Sort or filter cards. " +
                   "Escape: Close deck view.";
        }
    }
}
