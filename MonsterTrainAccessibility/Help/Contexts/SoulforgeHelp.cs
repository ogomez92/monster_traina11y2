namespace MonsterTrainAccessibility.Help.Contexts
{
    public class SoulforgeHelp : IHelpContext
    {
        public string ContextId => "soulforge";
        public string ContextName => "Soulforge";
        public int Priority => 70;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Soulforge;
        }

        public string GetHelpText()
        {
            return "Up and Down arrows: Navigate options. Enter: Select or craft. Escape: Go back. F5: Re-read current item. F1: Help.";
        }
    }
}
