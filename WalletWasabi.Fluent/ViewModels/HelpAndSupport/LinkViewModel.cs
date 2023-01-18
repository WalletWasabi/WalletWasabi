using System.Windows.Input;
using Avalonia;
using CommunityToolkit.Mvvm.Input;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

public class LinkViewModel : ViewModelBase
{
	public LinkViewModel()
	{
		OpenBrowserCommand = new AsyncRelayCommand<string>(IoHelpers.OpenBrowserAsync); // TODO RelayCommand: nullable

		CopyLinkCommand = new AsyncRelayCommand<string>(
			async (link) =>
				{
					if (Application.Current is { Clipboard: { } clipboard })
					{
						await clipboard.SetTextAsync(link); // TODO RelayCommand: nullable
					}
				});
	}

	public string? Link { get; set; }

	public string? Description { get; set; }

	public bool IsClickable { get; set; }

	public ICommand OpenBrowserCommand { get; }

	public ICommand CopyLinkCommand { get; }
}
