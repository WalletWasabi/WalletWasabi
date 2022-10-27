using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public class ConstantTemplate<T> : IDataTemplate
{
	private readonly Func<T, object> _build;
	private readonly HorizontalAlignment _alignment;

	public ConstantTemplate(Func<T, object> build, HorizontalAlignment alignment = HorizontalAlignment.Center)
	{
		_build = build;
		_alignment = alignment;
	}

	public IControl Build(object param)
	{
		return new ContentPresenter
		{
			Content = _build((T) param),
			HorizontalContentAlignment = _alignment
		};
	}

	public bool Match(object data)
	{
		return data is T;
	}
}
