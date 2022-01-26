using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Controls;

public class TileControl : ContentControl
{
	private ContentPresenter? _contentPresenter;

	public static readonly StyledProperty<object?> LargeSizeContentProperty =
		AvaloniaProperty.Register<TileControl, object?>(nameof(LargeSizeContent));

	public static readonly StyledProperty<object?> WideSizeContentProperty =
		AvaloniaProperty.Register<TileControl, object?>(nameof(WideSizeContent));

	public static readonly StyledProperty<TileSize> TileSizeProperty =
		AvaloniaProperty.Register<TileControl, TileSize>(nameof(TileSize));

	public object? LargeSizeContent
	{
		get => GetValue(LargeSizeContentProperty);
		set => SetValue(LargeSizeContentProperty, value);
	}

	public object? WideSizeContent
	{
		get => GetValue(WideSizeContentProperty);
		set => SetValue(WideSizeContentProperty, value);
	}

	public TileSize TileSize
	{
		get => GetValue(TileSizeProperty);
		set => SetValue(TileSizeProperty, value);
	}

	private void UpdateContent()
	{
		if (_contentPresenter is { })
		{
			_contentPresenter.Content = GetClosestSizedContent();
		}
	}

	private object GetClosestSizedContent()
	{
		int currentSize = (int)TileSize;

		while (currentSize > 0)
		{
			var tileSize = (TileSize)currentSize;

			switch (tileSize)
			{
				case TileSize.Wide:
					if (WideSizeContent is { })
					{
						return WideSizeContent;
					}
					break;

				case TileSize.Large:
					if (LargeSizeContent is { })
					{
						return LargeSizeContent;
					}
					break;

			}

			currentSize--;
		}

		return Content;
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		_contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");

		UpdateContent();
	}

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == TileSizeProperty)
		{
			UpdateContent();
		}
	}
}
