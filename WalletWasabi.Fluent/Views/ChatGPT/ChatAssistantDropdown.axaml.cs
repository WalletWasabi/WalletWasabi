using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.ChatGPT;

public partial class ChatAssistantDropdown : UserControl
{
	private bool _scrollToEnd;

	public ChatAssistantDropdown()
	{
		InitializeComponent();

		var messagesItemsControl = this.FindControl<SelectingItemsControl>("MessagesItemsControl");
		var chatScrollViewer = this.FindControl<ScrollViewer>("ChatScrollViewer");

		messagesItemsControl
			.GetObservable(SelectingItemsControl.SelectedItemProperty)
			.Subscribe(_ =>
			{
				_scrollToEnd = true;
			});

		chatScrollViewer
			.GetObservable(ScrollViewer.ExtentProperty)
			.Subscribe(_ =>
			{
				if (_scrollToEnd)
				{
					chatScrollViewer.ScrollToEnd();
					_scrollToEnd = false;
				}
			});
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
