namespace MonsterTrainAccessibility.Help.Contexts
{
    public class SoulSaviorHelp : IHelpContext
    {
        public string ContextId => "soul_savior";
        public string ContextName => "Soul Savior Map";
        public int Priority => 60;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.SoulSaviorMap;
        }

        public string GetHelpText()
        {
            return "Arrow keys: Navigate the map. Enter: Select a node. Escape: Go back. F5: Re-read current item. F1: Help.";
        }
    }
}
