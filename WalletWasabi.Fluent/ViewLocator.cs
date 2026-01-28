using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent;

[StaticViewLocator]
public partial class ViewLocator : IDataTemplate
{
	public Control Build(object? data)
	{
		var type = data?.GetType();

		if (type is not null && s_views.TryGetValue(type, out var func))
		{
			return func.Invoke();
		}

		throw new Exception($"Unable to create view for type: {type}");
	}

	public bool Match(object? data) => data is ViewModelBase;
}
