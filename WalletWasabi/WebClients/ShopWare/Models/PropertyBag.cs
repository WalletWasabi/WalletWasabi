using System.Collections.Generic;

namespace WalletWasabi.WebClients.ShopWare.Models;

public class PropertyBag : Dictionary<string, object>
{
	public static readonly PropertyBag Empty = new();
}
