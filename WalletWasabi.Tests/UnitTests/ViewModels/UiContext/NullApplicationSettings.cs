using NBitcoin;
using WalletWasabi.Daemon;

namespace WalletWasabi.Fluent.Models.UI;

public class NullApplicationSettings : IApplicationSettings
{
	public IObservable<bool> IsRestartNeeded => throw new NotImplementedException();

	public bool EnableGpu { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public Network Network { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool StartLocalBitcoinCoreOnStartup { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public string LocalBitcoinCoreDataDir { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool StopLocalBitcoinCoreOnShutdown { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public string BitcoinP2PEndPoint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public string DustThreshold { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool DarkModeEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool AutoCopy { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool AutoPaste { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool CustomChangeAddress { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public FeeDisplayUnit SelectedFeeDisplayUnit { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool RunOnSystemStartup { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool HideOnClose { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool UseTor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool TerminateTorOnExit { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool DownloadNewVersion { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool PrivacyMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

	public bool CheckIfRestartIsNeeded(PersistentConfig config)
	{
		throw new NotImplementedException();
	}
}
