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

			switch(presenter.Name)
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
