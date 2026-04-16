namespace MonsterTrainAccessibility.Help.Contexts
{
    public class MutatorDraftHelp : IHelpContext
    {
        public string ContextId => "mutator_draft";
        public string ContextName => "Mutator Draft";
        public int Priority => 80;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.MutatorDraft;
        }

        public string GetHelpText()
        {
            return "Up and Down arrows: Browse mutators. Enter: Select or toggle a mutator. Escape: Confirm and continue. F5: Re-read current mutator. F1: Help.";
        }
    }
}
