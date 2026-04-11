namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the settings screen
    /// </summary>
    public class SettingsHelp : IHelpContext
    {
        public string ContextId => "settings";
        public string ContextName => "Settings";
        public int Priority => 55;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Settings;
        }

        public string GetHelpText()
        {
            return "Up and Down arrows: Navigate settings. " +
                   "Left and Right arrows: Change setting values. " +
                   "Tab: Switch between tabs. " +
                   "Enter: Confirm selection. " +
                   "Escape: Close settings.";
        }
    }
}
