namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for battle/combat (when not targeting)
    /// </summary>
    public class BattleHelp : IHelpContext
    {
        public string ContextId => "battle";
        public string ContextName => "Battle";
        public int Priority => 90;

        public bool IsActive()
        {
            // Active during battle, but not when floor targeting is active
            if (ScreenStateTracker.CurrentScreen != GameScreen.Battle)
                return false;

            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle == null || !battle.IsInBattle)
                return false;

            // Check if floor targeting is NOT active
            var targeting = MonsterTrainAccessibility.FloorTargeting;
            return targeting == null || !targeting.IsTargeting;
        }

        public string GetHelpText()
        {
            return "Battle screen. Play cards to defeat enemies and protect your Pyre. " +
                   "F7: Read hand (all cards with costs and effects). " +
                   "F2: Read floors (friendly and enemy units on each floor). " +
                   "F3: Read all units (your monsters and enemies on each floor). " +
                   "L: Show card preview for the selected card. " +
                   "N: Toggle game speed, announces the new speed. " +
                   "R: Read resources (ember, gold, pyre health, pyre attack). " +
                   "Enter: Play selected card. Some cards require floor selection. " +
                   "F12: End your turn and start combat phase. " +
                   "O: Cycle abilities (Pyre and unit abilities including champion). P: Activate the focused ability. " +
                   "F5: Re-read current selection. " +
                   "F6: Read all visible text on screen. " +
                   "F11: Cycle verbosity level (minimal, normal, verbose). " +
                   "Enemies ascend each turn. Protect your Pyre on top floor!";
        }
    }
}
