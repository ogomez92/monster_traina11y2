namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for champion upgrade selection screen.
    /// </summary>
    public class ChampionUpgradeHelp : IHelpContext
    {
        public string ContextId => "champion_upgrade";
        public string ContextName => "Champion Upgrade";
        public int Priority => 80; // Same as card draft

        private static bool _isActive = false;
        private static float _lastActivationTime = 0f;

        public static void SetActive(bool active)
        {
            _isActive = active;
            if (active)
            {
                _lastActivationTime = UnityEngine.Time.unscaledTime;
            }
            MonsterTrainAccessibility.LogInfo($"ChampionUpgradeHelp.SetActive({active})");
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
            return "Arrow keys: Browse upgrade options. " +
                   "Enter or Space: Select upgrade. " +
                   "C: Re-read current upgrade. " +
                   "T: Read all upgrade options. " +
                   "Each upgrade permanently enhances your champion.";
        }
    }
}
