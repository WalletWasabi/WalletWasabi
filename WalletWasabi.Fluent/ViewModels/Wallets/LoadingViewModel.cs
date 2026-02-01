using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(Title = null)]
public partial class LoadingViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	[AutoNotify] private double _percent;
	[AutoNotify] private string _statusText = " "; // Should not be empty as we have to preserve the space in the view.
	[AutoNotify] private string _timeToCatchUp;
	[AutoNotify] private bool _isLoading;

	public LoadingViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		_timeToCatchUp = "";
	}

	public string WalletName => _wallet.Name;

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		_wallet.Loader.Progress
					  .Do(p => UpdateStatus(p.RemainingFiltersToDownload, p.CurrentHeight, p.ChainTip, p.Percent))
					  .Subscribe()
					  .DisposeWith(disposables);
	}

	private void UpdateStatus(uint remainingFiltersToDownload, uint currentHeight, uint chainTip, double percentProgress)
	{
		if (remainingFiltersToDownload > 0)
		{
			StatusText = $"Downloading {remainingFiltersToDownload:N0} filters";
			return;
		}

		Percent = percentProgress;

		var remainingBlocks = chainTip - currentHeight;
		var hoursRemaining = remainingBlocks / 6.0m;

		// Convert hours to more readable format
		var remainingTimeString = hoursRemaining switch
		{
			< 1 => "less than 1 hour",
			< 24 => $"{Math.Ceiling(hoursRemaining)} hours",
			< 720 => $"{Math.Ceiling(hoursRemaining / 24)} days",
			< 8760 => $"{Math.Ceiling(hoursRemaining / 720)} months",
			_ => $"{Math.Ceiling(hoursRemaining / 8760)} years"
		};

		var blocksRemaining = chainTip - currentHeight;

		if (blocksRemaining == 0)
		{
			StatusText = "Done!";
			TimeToCatchUp = "";
			return;
		}

		StatusText = $"{blocksRemaining:N0} blocks remaining";
		TimeToCatchUp = $"{remainingTimeString} of Bitcoin history";
	}
}
