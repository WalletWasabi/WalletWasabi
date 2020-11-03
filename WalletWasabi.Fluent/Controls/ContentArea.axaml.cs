using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;

namespace WalletWasabi.Fluent.Controls
{
	public class ContentArea : ContentControl
	{
		public static readonly StyledProperty<object> TitleProperty =
			AvaloniaProperty.Register<ContentArea, object>(nameof(Title));

		public static readonly StyledProperty<object> CaptionProperty =
			AvaloniaProperty.Register<ContentArea, object>(nameof(Caption));

		private IContentPresenter? _titlePresenter;
		private IContentPresenter? _captionPresenter;

		public object Title
		{
			get => GetValue(TitleProperty);
			set => SetValue(TitleProperty, value);
		}

		public object Caption
		{
			get => GetValue(CaptionProperty);
			set => SetValue(CaptionProperty, value);
		}

		protected override bool RegisterContentPresenter(IContentPresenter presenter)
		{
			var result = base.RegisterContentPresenter(presenter);

			switch (presenter.Name)
			{
				case "PART_TitlePresenter":
					if (_titlePresenter is { })
					{
						_titlePresenter.PropertyChanged -= PresenterOnPropertyChanged;
					}

					_titlePresenter = presenter;
					_titlePresenter.PropertyChanged += PresenterOnPropertyChanged;
					result = true;
					break;

				case "PART_CaptionPresenter":
					if (_captionPresenter is { })
					{
						_captionPresenter.PropertyChanged -= PresenterOnPropertyChanged;
					}

					_captionPresenter = presenter;
					_captionPresenter.PropertyChanged += PresenterOnPropertyChanged;
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

				if (e.OldValue is IStyledElement oldValue)
				{
					oldValue.Classes.Remove(className);
				}

				if (e.NewValue is IStyledElement newValue)
				{
					newValue.Classes.Add(className);
				}
			}
		}
	}
}
