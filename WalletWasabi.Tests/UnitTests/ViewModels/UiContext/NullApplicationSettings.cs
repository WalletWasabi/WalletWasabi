using Avalonia.Controls;
using NBitcoin;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Tests.UnitTests.ViewModels.UIContext;

public class NullApplicationSettings : IApplicationSettings
{
	public bool IsOverridden => throw new NotImplementedException();
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
	public bool Oobe { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public WindowState WindowState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool DoUpdateOnClose { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public bool ShowBuyAnythingInfo { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

	public bool CheckIfRestartIsNeeded(PersistentConfig config)
	{
		throw new NotImplementedException();
	}
}
