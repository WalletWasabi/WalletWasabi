using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar;

namespace WalletWasabi.Fluent.Views.SearchBar;

public class SearchBar : UserControl
{
	private readonly CompositeDisposable _disposables = new();

	public SearchBar()
	{
		InitializeComponent();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		MessageBus.Current
			.Listen<CloseSearchBarMessage>()
			.Do(_ => FocusMainWindow())
			.Subscribe()
			.DisposeWith(_disposables);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		_disposables.Dispose();
	}

	private static void FocusMainWindow()
	{
		if (Application.Current is { ApplicationLifetime: IClassicDesktopStyleApplicationLifetime applicationLifetime })
		{
			applicationLifetime.MainWindow.Focus();
		}
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
