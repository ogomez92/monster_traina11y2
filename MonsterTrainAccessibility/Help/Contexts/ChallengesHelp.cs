namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for challenges/daily challenges screens.
    /// </summary>
    public class ChallengesHelp : IHelpContext
    {
        public string ContextId => "challenges";
        public string ContextName => "Challenges";
        public int Priority => 45; // Just above main menu (40)

        private static bool _isActive = false;
        private static float _lastActivationTime = 0f;

        public static void SetActive(bool active)
        {
            _isActive = active;
            if (active)
            {
                _lastActivationTime = UnityEngine.Time.unscaledTime;
            }
            MonsterTrainAccessibility.LogInfo($"ChallengesHelp.SetActive({active})");
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
            return "Arrow keys: Browse available challenges. " +
                   "Enter or Space: View challenge details or start challenge. " +
                   "Tab: Switch between daily and weekly challenges. " +
                   "C: Re-read current challenge. " +
                   "T: Read challenge rules and rewards. " +
                   "Escape: Return to main menu.";
        }
    }
}
