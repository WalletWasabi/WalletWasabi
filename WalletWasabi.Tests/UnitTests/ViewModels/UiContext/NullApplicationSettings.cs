using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using Markdown.Avalonia.Utils;
using NBitcoin;
using WalletWasabi.Daemon.Configuration;
using WalletWasabi.Discoverability;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Models;

namespace WalletWasabi.Tests.UnitTests.ViewModels.UIContext;

public class NullApplicationSettings : IApplicationSettings
{
	public bool IsOverridden { get; } = false;
	public ICommand RunOnSystemStartupCommand { get; } = new DefaultHyperlinkCommand();
	public IObservable<bool> IsRestartNeeded { get; } = Observable.Return(false);
	public bool EnableGpu { get; set; }
	public Network Network { get; set; } = Network.RegTest;
	public bool UseBitcoinRpc { get; set; }
	public string BitcoinRpcUri { get; set; } = "";
	public string BitcoinRpcCredentialString { get; set; } = "";
	public string RegTestCoordinatorUri { get; set; } = "";
	public string MaxCoinJoinMiningFeeRate { get; set; } = "";
	public string AbsoluteMinInputCount { get; set; } = "";
	public string IndexerUri { get; set; } = "";
	public string DustThreshold { get; set; } = "";
	public string ExchangeRateProvider { get; set; } = "";
	public string FeeRateEstimationProvider { get; set; } = "";
	public string ExternalTransactionBroadcaster { get; set; } = "";
	public string CoordinatorUri { get; set; } = "";
	public bool DarkModeEnabled { get; set; }
	public bool AutoCopy { get; set; }
	public bool AutoPaste { get; set; }
	public bool CustomChangeAddress { get; set; }
	public FeeDisplayUnit SelectedFeeDisplayUnit { get; set; }
	public bool RunOnSystemStartup { get; set; }
	public bool HideOnClose { get; set; }
	public TorMode UseTor { get; set; }
	public bool TerminateTorOnExit { get; set; }
	public bool DownloadNewVersion { get; set; }
	public bool PrivacyMode { get; set; }
	public bool Oobe { get; set; }
	public Version LastVersionHighlightsDisplayed { get; set; } = new Version();
	public WindowState WindowState { get; set; }
	public bool DoUpdateOnClose { get; set; }
	public string[] ExperimentalFeatures { get; set; } = [];

	public bool CheckIfRestartIsNeeded(PersistentConfig config)
	{
		return false;
	}

	public bool TryProcessCoordinatorConnectionString(CoordinatorConnectionString coordinatorConnectionString)
	{
		return false;
	}

	public bool TrySetCoordinatorUri(string uri, Network? network = null)
	{
		return false;
	}

	public void ResetToDefault()
	{
		throw new NotImplementedException();
	}
}
