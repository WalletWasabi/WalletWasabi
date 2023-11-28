using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Wallets.Buy;

public partial class OrderMessagesView : UserControl
{
	private bool _scrollToEnd;

	public OrderMessagesView()
	{
		InitializeComponent();

		var messagesItemsControl = this.FindControl<SelectingItemsControl>("MessagesItemsControl");
		var messagesScrollViewer = this.FindControl<ScrollViewer>("MessagesScrollViewer");

		messagesItemsControl
			.GetObservable(SelectingItemsControl.SelectedItemProperty)
			.Subscribe(_ =>
			{
				_scrollToEnd = true;
			});

		messagesScrollViewer
			.GetObservable(ScrollViewer.ExtentProperty)
			.Subscribe(_ =>
			{
				if (_scrollToEnd)
				{
					messagesScrollViewer.ScrollToEnd();
					_scrollToEnd = false;
				}
			});

		this.GetObservable(BoundsProperty)
			.Subscribe(_ =>
			{
				messagesScrollViewer.ScrollToEnd();
			});
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
