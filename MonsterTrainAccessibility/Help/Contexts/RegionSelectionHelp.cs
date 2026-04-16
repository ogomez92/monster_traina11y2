namespace MonsterTrainAccessibility.Help.Contexts
{
    public class RegionSelectionHelp : IHelpContext
    {
        public string ContextId => "region_selection";
        public string ContextName => "Region Selection";
        public int Priority => 50;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.RegionSelection;
        }

        public string GetHelpText()
        {
            return "Up and Down arrows: Browse available regions. Enter: Select region. Escape: Go back. F5: Re-read current item. F1: Help.";
        }
    }
}
