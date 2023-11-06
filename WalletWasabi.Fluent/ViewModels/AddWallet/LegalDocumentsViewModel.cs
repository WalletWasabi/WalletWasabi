using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(
	Title = "Legal Document",
	Caption = "Display terms and conditions",
	Order = 3,
	Category = "General",
	Keywords = new[] { "View", "Legal", "Document", "Terms", "Conditions", "Privacy", "Policy", "Statement" },
	IconName = "info_regular",
	Searchable = true,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class LegalDocumentsViewModel : RoutableViewModel
{
	[AutoNotify] private string? _content;

	private LegalDocumentsViewModel()
	{
		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

		EnableBack = true;

		NextCommand = BackCommand;
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (isInHistory)
		{
			return;
		}

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			try
			{
				IsBusy = true;
				var document = await UiContext.LegalDocumentsProvider.WaitAndGetLatestDocumentAsync();
				Content = document.Content;
			}
			catch (Exception ex)
			{
				var caption = "Failed to get Legal documents.";
				Logger.LogError(caption, ex);
				await ShowErrorAsync(Title, message: caption, caption: "");
				Navigate().Back();
			}
			finally
			{
				IsBusy = false;
			}
		});
	}
}
