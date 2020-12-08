// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent
{
	[StaticViewLocator]
	public partial class ViewLocator : IDataTemplate
	{
		public bool SupportsRecycling => false;

		public IControl Build(object data)
		{
			var type =  data.GetType();
			if (s_views.TryGetValue(type, out var func))
			{
				return func?.Invoke();
			}
			throw new Exception($"Unable to create view for type: {type}");
			/*var name =.FullName!.Replace("ViewModel", "View");
			var type = Type.GetType(name);

			if (type != null)
			{
				var result = Activator.CreateInstance(type) as Control;

				if (result is null)
				{
					throw new Exception($"Unable to activate type: {type}");
				}

				return result;
			}
			else
			{
				return new TextBlock { Text = "Not Found: " + name };
			}*/
		}

		public bool Match(object data)
		{
			return data is ViewModelBase;
		}
	}
}