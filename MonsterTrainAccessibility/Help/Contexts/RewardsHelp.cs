namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for rewards screen (after battle rewards).
    /// </summary>
    public class RewardsHelp : IHelpContext
    {
        public string ContextId => "rewards";
        public string ContextName => "Rewards";
        public int Priority => 75; // Between shop (70) and card draft (80)

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Rewards;
        }

        public string GetHelpText()
        {
            return "Arrow keys: Browse reward options. " +
                   "Enter or Space: Select reward. " +
                   "F5: Re-read current reward. " +
                   "F6: Read all rewards. " +
                   "Escape: Skip remaining rewards.";
        }
    }
}
