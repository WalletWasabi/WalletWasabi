using NBitcoin;

namespace WalletWasabi.Fluent.Models.UI;

public interface IApplicationSettings
{
	IObservable<bool> IsRestartNeeded { get; }

	public bool EnableGpu { get; set; }
	public Network Network { get; set; }
	public bool StartLocalBitcoinCoreOnStartup { get; set; }
	public string LocalBitcoinCoreDataDir { get; set; }
	public bool StopLocalBitcoinCoreOnShutdown { get; set; }
	public string BitcoinP2PEndPoint { get; set; }
	public string DustThreshold { get; set; }
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
}
