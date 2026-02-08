namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for compendium/logbook screen.
    /// </summary>
    public class CompendiumHelp : IHelpContext
    {
        public string ContextId => "compendium";
        public string ContextName => "Compendium";
        public int Priority => 50; // Same as clan selection

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Compendium;
        }

        public string GetHelpText()
        {
            return "Tab: Switch between categories (Cards, Clans, Artifacts, etc.). " +
                   "Arrow keys: Browse items in current category. " +
                   "Enter or Space: View item details. " +
                   "C: Re-read current item. " +
                   "T: Read all items on screen. " +
                   "Escape: Return to previous menu.";
        }
    }
}
