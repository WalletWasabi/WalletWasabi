using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(Title = "")]
public partial class LoadingViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	[AutoNotify] private double _percent;
	[AutoNotify] private string _statusText = " "; // Should not be empty as we have to preserve the space in the view.
	[AutoNotify] private string _timeToCatchUp;
	[AutoNotify] private bool _isLoading;
	[AutoNotify] private int _peers;
	[AutoNotify] private uint _blockHeaders;
	[AutoNotify] private uint _filterHeaders;
	[AutoNotify] private uint _compactFilters;
	[AutoNotify] private uint _blocks;
	[AutoNotify] private uint _latestBlock;

	public LoadingViewModel(UiContext uiContext, IWalletModel wallet) : base(uiContext)
	{
		_wallet = wallet;
		_timeToCatchUp = "";
	}

	public string WalletName => _wallet.Name;

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		_wallet.Loader.Progress
			.Do(p => UpdateStatus(p))
			.Subscribe()
			.DisposeWith(disposables);
	}

	private void UpdateStatus(WalletLoadProgress progress)
	{
		Peers = progress.Peers;
		BlockHeaders = progress.BlockHeaders;
		FilterHeaders = progress.FilterHeaders;
		CompactFilters = progress.CompactFilters;
		Blocks = progress.Blocks;
		LatestBlock = progress.ChainTip;

		if (progress.RemainingFiltersToDownload > 0)
		{
			StatusText = $"Downloading {progress.RemainingFiltersToDownload:N0} filters";
			return;
		}

		Percent = progress.Percent;

		if (progress.ChainTip < progress.CurrentHeight)
		{
			StatusText = "Initializing blockchain data…";
			TimeToCatchUp = "";
			return;
		}

		var remainingBlocks = progress.ChainTip - progress.CurrentHeight;
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

		if (remainingBlocks == 0)
		{
			StatusText = "Done!";
			TimeToCatchUp = "";
			return;
		}

		StatusText = $"{remainingBlocks:N0} blocks remaining";
		TimeToCatchUp = $"{remainingTimeString} of Bitcoin history";
	}
}
