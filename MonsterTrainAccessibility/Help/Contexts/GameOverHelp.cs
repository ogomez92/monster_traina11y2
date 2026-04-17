namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the defeat / victory screen shown at run end.
    /// </summary>
    public class GameOverHelp : IHelpContext
    {
        public string ContextId => "gameover";
        public string ContextName => "Run Summary";
        public int Priority => 80;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Victory
                || ScreenStateTracker.CurrentScreen == GameScreen.Defeat;
        }

        public string GetHelpText()
        {
            return "Run summary screen. " +
                   "F5: Re-read summary. " +
                   "F6: Read all records and objectives. " +
                   "Q: Back to outpost. " +
                   "F: New run. " +
                   "Tab: Run summary details.";
        }
    }
}
