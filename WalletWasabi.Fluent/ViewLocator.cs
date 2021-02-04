using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent
{
	[StaticViewLocator]
	public partial class ViewLocator : IDataTemplate
	{
		public IControl Build(object data)
		{
			var type = data.GetType();
			if (s_views.TryGetValue(type, out var func))
			{
				return func.Invoke();
			}
			throw new Exception($"Unable to create view for type: {type}");
		}

		public bool Match(object data)
		{
			return data is ViewModelBase;
		}
	}
}
