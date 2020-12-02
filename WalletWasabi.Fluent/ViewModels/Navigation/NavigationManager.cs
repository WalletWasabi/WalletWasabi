using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public static class NavigationManager
	{
		private class InstanceGeneratorBase
		{
		}

		private class AsyncInstanceGenerator : InstanceGeneratorBase
		{
			public AsyncInstanceGenerator(Func<Task<RoutableViewModel>> generate)
			{
				Generate = generate;
			}

			public Func<Task<RoutableViewModel>> Generate { get; }
		}

		private class SynchronousInstanceGenerator : InstanceGeneratorBase
		{
			public SynchronousInstanceGenerator(Func<RoutableViewModel> generate)
			{
				Generate = generate;
			}

			public Func<RoutableViewModel> Generate { get; }
		}

		private class InstanceGenerator : InstanceGeneratorBase
		{
			public InstanceGenerator(RoutableViewModel generate)
			{
				Generate = generate;
			}

			public RoutableViewModel Generate { get; }
		}

		private static readonly Dictionary<NavigationMetaData, InstanceGeneratorBase> _navigationEntries = new();

		public static async Task<RoutableViewModel> MaterialiseViewModel(NavigationMetaData metaData)
		{
			if (_navigationEntries.ContainsKey(metaData))
			{
				if (_navigationEntries[metaData] is InstanceGenerator instanceGenerator)
				{
					return instanceGenerator.Generate;
				}
				else if (_navigationEntries[metaData] is SynchronousInstanceGenerator synchronousInstanceGenerator)
				{
					return synchronousInstanceGenerator.Generate();
				}
				else if (_navigationEntries[metaData] is AsyncInstanceGenerator asyncInstanceGenerator)
				{
					return await asyncInstanceGenerator.Generate();
				}
			}

			throw new Exception("ViewModel metadata not registered.");
		}

		public static IEnumerable<NavigationMetaData> MetaData => _navigationEntries.Keys.Select(x => x);

		public static void RegisterAsyncLazy(NavigationMetaData metaData, Func<Task<RoutableViewModel>> generator)
		{
			if (metaData.Searchable && (metaData.Category is null || metaData.Title is null))
			{
				throw new Exception("Searchable entries must have both a Category and a Title");
			}

			if (!_navigationEntries.ContainsKey(metaData))
			{
				_navigationEntries.Add(metaData, new AsyncInstanceGenerator(generator));
			}
		}

		public static void RegisterLazy(NavigationMetaData metaData, Func<RoutableViewModel> generator)
		{
			if (metaData.Searchable && (metaData.Category is null || metaData.Title is null))
			{
				throw new Exception("Searchable entries must have both a Category and a Title");
			}

			if (!_navigationEntries.ContainsKey(metaData))
			{
				_navigationEntries.Add(metaData, new SynchronousInstanceGenerator(generator));
			}
		}

		public static void Register(NavigationMetaData metaData, RoutableViewModel instance)
		{
			if (metaData.Searchable && (metaData.Category is null || metaData.Title is null))
			{
				throw new Exception("Searchable entries must have both a Category and a Title");
			}

			if (!_navigationEntries.ContainsKey(metaData))
			{
				_navigationEntries.Add(metaData, new InstanceGenerator(instance));
			}
		}
	}
}