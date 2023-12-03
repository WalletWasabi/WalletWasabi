using System.ComponentModel;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

internal static class ProductHelper
{
	public static string GetDescription(BuyAnythingClient.Product product)
	{
		var fieldInfo = product.GetType().GetField(product.ToString());
		var attribArray = fieldInfo!.GetCustomAttributes(false);

		if (attribArray.Length == 0)
		{
			return product.ToString();
		}

		DescriptionAttribute? attrib = null;

		foreach (var att in attribArray)
		{
			if (att is DescriptionAttribute attribute)
			{
				attrib = attribute;
			}
		}

		return attrib == null ? product.ToString() : attrib.Description;
	}
}
