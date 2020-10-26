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

		public static readonly StyledProperty<bool> EnableCancelProperty =
			AvaloniaProperty.Register<ContentArea, bool>(nameof(EnableCancel));

		public static readonly StyledProperty<bool> EnableNextProperty =
			AvaloniaProperty.Register<ContentArea, bool>(nameof(EnableNext));

		public static readonly StyledProperty<object> CancelContentProperty =
			AvaloniaProperty.Register<ContentArea, object>(nameof(CancelContent), "Cancel");

		public static readonly StyledProperty<object> NextContentProperty =
			AvaloniaProperty.Register<ContentArea, object>(nameof(NextContent), "Next");

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

		protected override bool RegisterContentPresenter(IContentPresenter presenter)
		{
			var result = base.RegisterContentPresenter(presenter);

			switch (presenter.Name)
			{
				case "PART_TitlePresenter":
				case "PART_CaptionPresenter":
					result = true;
					break;
			}

			return result;
		}
	}
}
