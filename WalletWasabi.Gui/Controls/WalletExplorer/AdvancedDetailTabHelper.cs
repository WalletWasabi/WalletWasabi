using System.Collections.Generic;
using System.Reflection;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public static class AdvancedDetailTabHelper
	{
		private static IEnumerable<(PropertyInfo, AdvancedDetailAttribute)> PropertiesWithAdvancedDetailAttribute(object instance)
		{
			foreach (PropertyInfo pInfo in ReflectionHelper.GetPropertyInfos(instance))
			{
				var vma = ReflectionHelper.GetAttribute<AdvancedDetailAttribute>(pInfo);
				if (vma != null)
				{
                    yield return (pInfo, vma);
				}
 			}
		}

		public static AdvancedDetailTabViewModel GenerateAdvancedDetailTab<T>(string Title, T targetVM) where T : ViewModelBase
		{
            var getAttr = PropertiesWithAdvancedDetailAttribute(targetVM);
			return new AdvancedDetailTabViewModel(Title, getAttr);
		}
	}
}