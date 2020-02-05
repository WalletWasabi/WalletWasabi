using System.Collections.Generic;
using System.Reflection;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public static class CoinInfoTabHelper
	{
		private static IEnumerable<AdvancedDetailPair> PropertiesWithAdvancedDetailAttribute(object instance)
		{
			foreach (PropertyInfo pInfo in ReflectionHelper.GetPropertyInfos(instance))
			{
				var vma = ReflectionHelper.GetAttribute<AdvancedDetailAttribute>(pInfo);
				if (vma != null)
				{
					yield return new AdvancedDetailPair(vma.DetailTitle, vma.IsSensitive, pInfo.Name);
				}
			}
		}

		public static CoinInfoTabViewModel GenerateCoinInfoTab<T>(string Title, T targetVM) where T : ViewModelBase
		{
			var getAttr = PropertiesWithAdvancedDetailAttribute(targetVM);
			return new CoinInfoTabViewModel(Title, targetVM, getAttr);
		}
	}
}