using ReactiveUI;

namespace WalletWasabi.Fluent.Models.UI;

public class UiServices
{
	// Temporary solution. It should be removed once all controls use DI.
	public static UiServices Instance { get; private set; } = null!;

	private readonly UiConfig _uiConfig;

	private UiServices(UiConfig uiConfig)
	{
		_uiConfig = uiConfig;
		PrivacyModeChanged = _uiConfig.WhenAnyValue(x => x.PrivacyMode);
	}

	public IObservable<bool> PrivacyModeChanged { get; }

	// Internal access for ApplicationSettings which needs direct UiConfig access
	internal UiConfig UiConfig => _uiConfig;

	public static UiServices Create(UiConfig uiConfig)
	{
		Instance = new UiServices(uiConfig);
		return Instance;
	}

	public bool GetHideOnClose() => _uiConfig.HideOnClose;
	public double? GetWindowWidth() => _uiConfig.WindowWidth;
	public double? GetWindowHeight() => _uiConfig.WindowHeight;
	public void SetWindowWidth(double? width)
	{
		_uiConfig.WindowWidth = width;
		_uiConfig.ToFile();
	}

	public void SetWindowHeight(double? height)
	{
		_uiConfig.WindowHeight = height;
		_uiConfig.ToFile();
	}
	public string? GetLastSelectedWallet() => _uiConfig.LastSelectedWallet;

	public void SetLastSelectedWallet(string? walletName)
	{
		_uiConfig.LastSelectedWallet = walletName;
		_uiConfig.ToFile();
	}

	public bool GetPrivacyMode() => _uiConfig.PrivacyMode;
	public bool GetAutocopy() => _uiConfig.Autocopy;
	public bool GetAutoPaste() => _uiConfig.AutoPaste;
	public bool GetSendAmountConversionReversed() => _uiConfig.SendAmountConversionReversed;

	public void SetSendAmountConversionReversed(bool value)
	{
		_uiConfig.SendAmountConversionReversed = value;
		_uiConfig.ToFile();
	}

	public int GetFeeTarget() => _uiConfig.FeeTarget;

	public void SetFeeTarget(int value)
	{
		_uiConfig.FeeTarget = value;
		_uiConfig.ToFile();
	}
}
