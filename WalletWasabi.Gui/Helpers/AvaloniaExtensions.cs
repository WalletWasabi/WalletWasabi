using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using AvalonStudio.Shell;
using Avalonia.Controls;

namespace Avalonia
{
	public static class AvaloniaExtensions
	{
		public static void AddOrReplace(this IResourceDictionary resources, object key, object value)
		{
			if (resources.TryGetResource(key.ToString(), out _))
				resources[key] = value;
			else
				resources.Add(key, value);
		}
	}
}
