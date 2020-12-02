using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public static class NavigationManager
	{
		private static Dictionary<NavigationMetaData, Func<Task<RoutableViewModel>>> _navigationEntries = new();

		public static async Task<RoutableViewModel> MaterialiseViewModel(NavigationMetaData metaData)
		{
			if (_navigationEntries.ContainsKey(metaData))
			{
				return await _navigationEntries[metaData]();
			}

			throw new Exception("ViewModel metadata not registered.");
		}

		public static IEnumerable<NavigationMetaData> MetaData => _navigationEntries.Keys.Select(x => x);

		public static void RegisterRoutable(NavigationMetaData metaData, Func<Task<RoutableViewModel>> generator)
		{
			if (metaData.Searchable && (metaData.Category is null || metaData.Title is null))
			{
				throw new Exception("Searchable entries must have both a Category and a Title");
			}

			if (!_navigationEntries.ContainsKey(metaData))
			{
				_navigationEntries.Add(metaData, generator);
			}
		}
	}
}