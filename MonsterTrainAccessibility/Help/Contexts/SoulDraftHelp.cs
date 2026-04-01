namespace MonsterTrainAccessibility.Help.Contexts
{
    public class SoulDraftHelp : IHelpContext
    {
        public string ContextId => "soul_draft";
        public string ContextName => "Soul Draft";
        public int Priority => 80;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.SoulDraft;
        }

        public string GetHelpText()
        {
            return "Left and Right arrows: Browse available souls. Enter: Select a soul. Escape: Cancel. C: Re-read current soul. F1: Help.";
        }
    }
}
