namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for artifact/relic selection screen.
    /// </summary>
    public class ArtifactSelectionHelp : IHelpContext
    {
        public string ContextId => "artifact_selection";
        public string ContextName => "Artifact Selection";
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
            MonsterTrainAccessibility.LogInfo($"ArtifactSelectionHelp.SetActive({active})");
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
            return "Arrow keys: Browse artifact choices. " +
                   "Enter or Space: Select artifact. " +
                   "F5: Re-read current artifact. " +
                   "F6: Read all artifact options. " +
                   "Artifacts provide passive bonuses for the entire run.";
        }
    }
}
