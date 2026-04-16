namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the shop/merchant screen
    /// </summary>
    public class ShopHelp : IHelpContext
    {
        public string ContextId => "shop";
        public string ContextName => "Shop";
        public int Priority => 70;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Shop;
        }

        public bool PassesAdditionalChecks()
        {
            return true;
        }

        public string GetHelpText()
        {
            return @"Shop Controls:
Arrow keys: Browse shop items
Enter: Purchase selected item
F5: Re-read current item and price
F6: Read all text on screen
Escape: Leave shop";
        }
    }
}
