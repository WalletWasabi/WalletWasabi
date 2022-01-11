using System.IO;
using System.Windows.Input;
using Avalonia;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

public class LinkViewModel : ViewModelBase
{
	public LinkViewModel()
	{
		OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(
			async (link) =>
				await IoHelpers.OpenBrowserAsync(link));

		CopyLinkCommand = ReactiveCommand.CreateFromTask<string>(
			async (link) =>
				{
					if (Application.Current is { Clipboard: { } clipboard })
					{
						await clipboard.SetTextAsync(link);
					}
				});
	}

	public string? Link { get; set; }

	public string? Description { get; set; }

	public bool IsClickable { get; set; }

	public ICommand OpenBrowserCommand { get; }

	public ICommand CopyLinkCommand { get; }
}
