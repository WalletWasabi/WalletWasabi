using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

public partial class LinkViewModel : ViewModelBase
{
	public LinkViewModel(UiContext uiContext) : base(uiContext)
	{
		OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(async (link) => await UiContext.FileSystem.OpenBrowserAsync(link));

		CopyLinkCommand = ReactiveCommand.CreateFromTask<string>(async (link) => await UiContext.Clipboard.SetTextAsync(link));
	}

	public string? Link { get; set; }

	public string? Description { get; set; }

	public bool IsClickable { get; set; }

	public ICommand OpenBrowserCommand { get; }

	public ICommand CopyLinkCommand { get; }
}
