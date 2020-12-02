using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.AddWallet;

namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public enum NavBarPosition
	{
		None,
		Top,
		Bottom
	}

	public class NavigationManager
	{
		struct NavigationEntry
		{
			public NavigationMetaData MetaData { get; set; }
			public Func<Task<RoutableViewModel>> GenerateViewModel { get; set; }
		}

		private static Dictionary<Type, NavigationEntry> _navigationTypes = new();

		public static IEnumerable<NavigationMetaData> MetaData => _navigationTypes.Values.Select(x => x.MetaData);

		public static void RegisterRoutable<T>(NavigationMetaData metaData, Func<Task<RoutableViewModel>> generator)
			where T : RoutableViewModel
		{
			if (!_navigationTypes.ContainsKey(typeof(T)))
			{
				_navigationTypes.Add(
					typeof(T),
					new NavigationEntry
					{
						GenerateViewModel = generator,
						MetaData = metaData
					});
			}
		}
	}
}