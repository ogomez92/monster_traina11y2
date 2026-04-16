namespace MonsterTrainAccessibility.Help.Contexts
{
    public class SoulProgressionHelp : IHelpContext
    {
        public string ContextId => "soul_progression";
        public string ContextName => "Soul Progression";
        public int Priority => 50;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.SoulProgression;
        }

        public string GetHelpText()
        {
            return "Up and Down arrows: Browse souls. Enter: View details. Escape: Go back. F5: Re-read current item. F1: Help.";
        }
    }
}
