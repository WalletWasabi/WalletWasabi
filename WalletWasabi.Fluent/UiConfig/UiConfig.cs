using Newtonsoft.Json;
using System.ComponentModel;
using System.IO;
using System.Reactive.Linq;
using System.Text.Json;
using ReactiveUI;
using WalletWasabi.Bases;
using WalletWasabi.Serialization;
using Unit = System.Reactive.Unit;

namespace WalletWasabi.Fluent;

public class UiConfig : ConfigBase
{
	private bool _privacyMode;
	private bool _isCustomChangeAddress;
	private bool _autocopy = true;
	private bool _darkModeEnabled = true;
	private string? _lastSelectedWallet;
	private string _windowState = "Normal";
	private bool _runOnSystemStartup;
	private bool _oobe = true;
	private Version _lastVersionHighlightsDisplayed = new (2, 3, 1);
	private bool _hideOnClose;
	private bool _autoPaste;
	private int _feeTarget = 2;
	private bool _sendAmountConversionReversed;
	private double? _windowWidth;
	private double? _windowHeight;

	public UiConfig() : base("./fakeUiConfig.for.testing.only.json")
	{
	}

	public UiConfig(string filePath) : base(filePath)
	{
		this.WhenAnyValue(
				x => x.Autocopy,
				x => x.AutoPaste,
				x => x.IsCustomChangeAddress,
				x => x.DarkModeEnabled,
				x => x.LastSelectedWallet,
				x => x.WindowState,
				x => x.Oobe,
				x => x.LastVersionHighlightsDisplayed,
				x => x.RunOnSystemStartup,
				x => x.PrivacyMode,
				x => x.HideOnClose,
				x => x.FeeTarget,
				(_, _, _, _, _, _, _, _, _, _, _, _) => Unit.Default)
			.Throttle(TimeSpan.FromMilliseconds(500))
			.Skip(1) // Won't save on UiConfig creation.
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => ToFile());

		this.WhenAnyValue(x => x.SendAmountConversionReversed)
			.Throttle(TimeSpan.FromMilliseconds(500))
			.Skip(1) // Won't save on UiConfig creation.
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => ToFile());

		this.WhenAnyValue(
				x => x.WindowWidth,
				x => x.WindowHeight)
			.Throttle(TimeSpan.FromMilliseconds(500))
			.Skip(1) // Won't save on UiConfig creation.
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(_ => ToFile());
	}

	public bool Oobe
	{
		get => _oobe;
		set => RaiseAndSetIfChanged(ref _oobe, value);
	}

	public Version LastVersionHighlightsDisplayed
	{
		get => _lastVersionHighlightsDisplayed;
		set => RaiseAndSetIfChanged(ref _lastVersionHighlightsDisplayed, value);
	}

	public string WindowState
	{
		get => _windowState;
		internal set => RaiseAndSetIfChanged(ref _windowState, value);
	}

	public int FeeTarget
	{
		get => _feeTarget;
		internal set => RaiseAndSetIfChanged(ref _feeTarget, value);
	}

	public bool Autocopy
	{
		get => _autocopy;
		set => RaiseAndSetIfChanged(ref _autocopy, value);
	}

	public bool AutoPaste
	{
		get => _autoPaste;
		set => RaiseAndSetIfChanged(ref _autoPaste, value);
	}

	public bool IsCustomChangeAddress
	{
		get => _isCustomChangeAddress;
		set => RaiseAndSetIfChanged(ref _isCustomChangeAddress, value);
	}

	public bool PrivacyMode
	{
		get => _privacyMode;
		set => RaiseAndSetIfChanged(ref _privacyMode, value);
	}

	public bool DarkModeEnabled
	{
		get => _darkModeEnabled;
		set => RaiseAndSetIfChanged(ref _darkModeEnabled, value);
	}

	public string? LastSelectedWallet
	{
		get => _lastSelectedWallet;
		set => RaiseAndSetIfChanged(ref _lastSelectedWallet, value);
	}

	// OnDeserialized changes this default on Linux.
	public bool RunOnSystemStartup
	{
		get => _runOnSystemStartup;
		set => RaiseAndSetIfChanged(ref _runOnSystemStartup, value);
	}

	public bool HideOnClose
	{
		get => _hideOnClose;
		set => RaiseAndSetIfChanged(ref _hideOnClose, value);
	}

	public bool SendAmountConversionReversed
	{
		get => _sendAmountConversionReversed;
		internal set => RaiseAndSetIfChanged(ref _sendAmountConversionReversed, value);
	}

	public double? WindowWidth
	{
		get => _windowWidth;
		internal set => RaiseAndSetIfChanged(ref _windowWidth, value);
	}

	public double? WindowHeight
	{
		get => _windowHeight;
		internal set => RaiseAndSetIfChanged(ref _windowHeight, value);
	}

	public static UiConfig LoadFile(string filePath)
	{
		try
		{
			using var cfgFile = File.Open(filePath, FileMode.Open, FileAccess.Read);
			var decoder = JsonDecoder.FromStream(UiConfigDecode.UiConfig(filePath));
			var decodingResult = decoder(cfgFile);
			return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException(error));
		}
		catch (Exception ex)
		{
			var config = new UiConfig(filePath);
			File.WriteAllTextAsync(filePath, config.EncodeAsJson());
			Logging.Logger.LogInfo($"{nameof(UiConfig)} file has been deleted because it was corrupted. Recreated default version at path: `{filePath}`.");
			Logging.Logger.LogWarning(ex);
			return config;
		}
	}

	protected override string EncodeAsJson() =>
		JsonEncoder.ToReadableString(this, UiConfigEncode.UiConfig);
}
