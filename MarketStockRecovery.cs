using System.Collections.Generic;

namespace SilverpineMods.CustomItemLoader;

internal static class MarketStockRecovery
{
    internal static int ReturnToCrate(Inventory source, Inventory crate, ISet<string> excluded)
    {
        // Native transfers check capacity and retain items that cannot move.
        // Never drop or destroy unsold stock just because its listing is gone.
        source.MoveAllItemsTo(crate, item => item != null && excluded.Contains(item.name));
        return source.GetItemsWithPredicate(item => item != null && excluded.Contains(item.name)).Count;
    }
}
