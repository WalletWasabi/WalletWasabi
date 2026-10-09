using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;

public class ContentArea : ContentControl
{
	public static readonly StyledProperty<object> TitleProperty =
		AvaloniaProperty.Register<ContentArea, object>(nameof(Title));

	public static readonly StyledProperty<object?> TopContentProperty =
		AvaloniaProperty.Register<ContentArea, object?>(nameof(TopContent));

	public static readonly StyledProperty<object?> BottomContentProperty =
		AvaloniaProperty.Register<ContentArea, object?>(nameof(BottomContent));

	public static readonly StyledProperty<object> CaptionProperty =
		AvaloniaProperty.Register<ContentArea, object>(nameof(Caption));

	public static readonly StyledProperty<bool> EnableBackProperty =
		AvaloniaProperty.Register<ContentArea, bool>(nameof(EnableBack));

	public static readonly StyledProperty<bool> EnableCancelProperty =
		AvaloniaProperty.Register<ContentArea, bool>(nameof(EnableCancel));

	public static readonly StyledProperty<bool> EnableNextProperty =
		AvaloniaProperty.Register<ContentArea, bool>(nameof(EnableNext));

	public static readonly StyledProperty<bool> EnableSkipProperty =
		AvaloniaProperty.Register<ContentArea, bool>(nameof(EnableSkip));

	public static readonly StyledProperty<bool> FocusNextProperty =
		AvaloniaProperty.Register<ContentArea, bool>(nameof(FocusNext));

	public static readonly StyledProperty<bool> FocusCancelProperty =
		AvaloniaProperty.Register<ContentArea, bool>(nameof(FocusCancel));

	public static readonly StyledProperty<object> CancelContentProperty =
		AvaloniaProperty.Register<ContentArea, object>(nameof(CancelContent), "Cancel");

	public static readonly StyledProperty<object> NextContentProperty =
		AvaloniaProperty.Register<ContentArea, object>(nameof(NextContent), "Next");

	public static readonly StyledProperty<object> SkipContentProperty =
		AvaloniaProperty.Register<ContentArea, object>(nameof(NextContent), "Skip");

	public static readonly StyledProperty<bool> IsBusyProperty =
		AvaloniaProperty.Register<ContentArea, bool>(nameof(IsBusy));

	public static readonly StyledProperty<IBrush> HeaderBackgroundProperty =
		AvaloniaProperty.Register<ContentArea, IBrush>(nameof(HeaderBackground));

	public static readonly StyledProperty<object?> NextButtonSubActionsProperty = AvaloniaProperty.Register<ContentArea, object?>(nameof(NextButtonSubActions));

	public static readonly StyledProperty<object> SubActionContentProperty =
		AvaloniaProperty.Register<ContentArea, object>(nameof(SubActionContent), "SubAction");

	private ContentPresenter? _titlePresenter;
	private ContentPresenter? _captionPresenter;

	public object Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public object? TopContent
	{
		get => GetValue(TopContentProperty);
		set => SetValue(TopContentProperty, value);
	}

	public object? BottomContent
	{
		get => GetValue(BottomContentProperty);
		set => SetValue(BottomContentProperty, value);
	}

	public object Caption
	{
		get => GetValue(CaptionProperty);
		set => SetValue(CaptionProperty, value);
	}

	public bool EnableBack
	{
		get => GetValue(EnableBackProperty);
		set => SetValue(EnableBackProperty, value);
	}

	public bool EnableCancel
	{
		get => GetValue(EnableCancelProperty);
		set => SetValue(EnableCancelProperty, value);
	}

	public bool EnableNext
	{
		get => GetValue(EnableNextProperty);
		set => SetValue(EnableNextProperty, value);
	}

	public bool EnableSkip
	{
		get => GetValue(EnableSkipProperty);
		set => SetValue(EnableSkipProperty, value);
	}

	public bool FocusNext
	{
		get => GetValue(FocusNextProperty);
		set => SetValue(FocusNextProperty, value);
	}

	public bool FocusCancel
	{
		get => GetValue(FocusCancelProperty);
		set => SetValue(FocusCancelProperty, value);
	}

	public object CancelContent
	{
		get => GetValue(CancelContentProperty);
		set => SetValue(CancelContentProperty, value);
	}

	public object NextContent
	{
		get => GetValue(NextContentProperty);
		set => SetValue(NextContentProperty, value);
	}

	public object SkipContent
	{
		get => GetValue(SkipContentProperty);
		set => SetValue(SkipContentProperty, value);
	}

	public bool IsBusy
	{
		get => GetValue(IsBusyProperty);
		set => SetValue(IsBusyProperty, value);
	}

	public IBrush HeaderBackground
	{
		get => GetValue(HeaderBackgroundProperty);
		set => SetValue(HeaderBackgroundProperty, value);
	}
	public object? NextButtonSubActions
	{
		get => GetValue(NextButtonSubActionsProperty);
		set => SetValue(NextButtonSubActionsProperty, value);
	}
	public object SubActionContent
	{
		get => GetValue(SubActionContentProperty);
		set => SetValue(SubActionContentProperty, value);
	}

	protected override bool RegisterContentPresenter(ContentPresenter presenter)
	{
		var result = base.RegisterContentPresenter(presenter);

		if (presenter is not { } contentPresenter)
		{
			return result;
		}

		switch (presenter.Name)
		{
			case "PART_TitlePresenter":
				if (_titlePresenter is { })
				{
					_titlePresenter.PropertyChanged -= PresenterOnPropertyChanged;
				}

				_titlePresenter = contentPresenter;
				_titlePresenter.PropertyChanged += PresenterOnPropertyChanged;
				result = true;
				break;

			case "PART_CaptionPresenter":
				if (_captionPresenter is { })
				{
					_captionPresenter.PropertyChanged -= PresenterOnPropertyChanged;
				}

				_captionPresenter = contentPresenter;
				_captionPresenter.PropertyChanged += PresenterOnPropertyChanged;
				_captionPresenter.IsVisible = Caption is not null;
				result = true;
				break;
		}

		return result;
	}

	private void PresenterOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == ContentPresenter.ChildProperty)
		{
			var className = sender == _captionPresenter ? "caption" : "title";

			if (e.OldValue is StyledElement oldValue)
			{
				oldValue.Classes.Remove(className);
			}

			if (e.NewValue is StyledElement newValue)
			{
				newValue.Classes.Add(className);
			}
		}
		else if (e.Property == CaptionProperty && _captionPresenter is not null)
		{
			_captionPresenter.IsVisible = e.NewValue is not null;
		}
	}
}
