using System.Collections.Generic;
using System.Reflection;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public static class AdvancedDetailTabHelper
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

		public static AdvancedDetailTabViewModel GenerateAdvancedDetailTab<T>(string Title, T targetVM) where T : ViewModelBase
		{
			var getAttr = PropertiesWithAdvancedDetailAttribute(targetVM);
			return new AdvancedDetailTabViewModel(Title, targetVM, getAttr);
		}
	}
}