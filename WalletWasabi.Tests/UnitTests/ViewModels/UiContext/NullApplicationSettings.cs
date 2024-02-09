using System.Reactive.Linq;
using Avalonia.Controls;
using NBitcoin;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Tests.UnitTests.ViewModels.UIContext;

public class NullApplicationSettings : IApplicationSettings
{
	public bool IsOverridden { get; } = false;
	public IObservable<bool> IsRestartNeeded { get; } = Observable.Return(false);
	public bool EnableGpu { get; set; }
	public Network Network { get; set; } = Network.RegTest;
	public bool StartLocalBitcoinCoreOnStartup { get; set; }
	public string LocalBitcoinCoreDataDir { get; set; } = "";
	public bool StopLocalBitcoinCoreOnShutdown { get; set; }
	public string BitcoinP2PEndPoint { get; set; } = "";
	public string DustThreshold { get; set; } = "";
	public bool DarkModeEnabled { get; set; }
	public bool AutoCopy { get; set; }
	public bool AutoPaste { get; set; }
	public bool CustomChangeAddress { get; set; }
	public FeeDisplayUnit SelectedFeeDisplayUnit { get; set; }
	public bool RunOnSystemStartup { get; set; }
	public bool HideOnClose { get; set; }
	public bool UseTor { get; set; }
	public bool TerminateTorOnExit { get; set; }
	public bool DownloadNewVersion { get; set; }
	public bool PrivacyMode { get; set; }
	public bool Oobe { get; set; }
	public WindowState WindowState { get; set; }
	public bool DoUpdateOnClose { get; set; }

	public bool CheckIfRestartIsNeeded(PersistentConfig config)
	{
		return false;
	}
}
