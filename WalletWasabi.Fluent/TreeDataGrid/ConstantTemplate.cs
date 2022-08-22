using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;

namespace WalletWasabi.Fluent.TreeDataGrid;

public class ConstantTemplate<T> : IDataTemplate
{
	private readonly Func<T, object> _build;

	public ConstantTemplate(Func<T, object> build)
	{
		_build = build;
	}

	public IControl Build(object param)
	{
		return new ContentPresenter() { Content = _build((T)param) };
	}

	public bool Match(object data)
	{
		return data is T;
	}
}
