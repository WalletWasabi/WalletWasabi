using Newtonsoft.Json;
using System.ComponentModel;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Bases;
using WalletWasabi.Fluent.Converters;
using Unit = System.Reactive.Unit;

namespace WalletWasabi.Fluent;

[JsonObject(MemberSerialization.OptIn)]
public class UiConfig : ConfigBase
{
	private bool _privacyMode;
	private bool _isCustomChangeAddress;
	private bool _autocopy;
	private bool _darkModeEnabled;
	private string? _lastSelectedWallet;
	private string _windowState = "Normal";
	private bool _runOnSystemStartup;
	private bool _oobe;
	private Version _lastVersionHighlightsDisplayed = new (2, 3, 1);
	private bool _hideOnClose;
	private bool _autoPaste;
	private int _feeTarget;
	private bool _sendAmountConversionReversed;
	private double? _windowWidth;
	private double? _windowHeight;

	public UiConfig() : base()
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

	[JsonProperty(PropertyName = "Oobe", DefaultValueHandling = DefaultValueHandling.Populate)]
	[DefaultValue(true)]
	public bool Oobe
	{
		get => _oobe;
		set => RaiseAndSetIfChanged(ref _oobe, value);
	}

	[JsonProperty(PropertyName = "LastVersionHighlightsDisplayed")]
	public Version LastVersionHighlightsDisplayed
	{
		get => _lastVersionHighlightsDisplayed;
		set => RaiseAndSetIfChanged(ref _lastVersionHighlightsDisplayed, value);
	}

	[JsonProperty(PropertyName = "WindowState")]
	[JsonConverter(typeof(WindowStateAfterStartJsonConverter))]
	public string WindowState
	{
		get => _windowState;
		internal set => RaiseAndSetIfChanged(ref _windowState, value);
	}

	[DefaultValue(2)]
	[JsonProperty(PropertyName = "FeeTarget", DefaultValueHandling = DefaultValueHandling.Populate)]
	public int FeeTarget
	{
		get => _feeTarget;
		internal set => RaiseAndSetIfChanged(ref _feeTarget, value);
	}

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "Autocopy", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool Autocopy
	{
		get => _autocopy;
		set => RaiseAndSetIfChanged(ref _autocopy, value);
	}

	[DefaultValue(false)]
	[JsonProperty(PropertyName = nameof(AutoPaste), DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool AutoPaste
	{
		get => _autoPaste;
		set => RaiseAndSetIfChanged(ref _autoPaste, value);
	}

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "IsCustomChangeAddress", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool IsCustomChangeAddress
	{
		get => _isCustomChangeAddress;
		set => RaiseAndSetIfChanged(ref _isCustomChangeAddress, value);
	}

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "PrivacyMode", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool PrivacyMode
	{
		get => _privacyMode;
		set => RaiseAndSetIfChanged(ref _privacyMode, value);
	}

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "DarkModeEnabled", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool DarkModeEnabled
	{
		get => _darkModeEnabled;
		set => RaiseAndSetIfChanged(ref _darkModeEnabled, value);
	}

	[DefaultValue(null)]
	[JsonProperty(PropertyName = "LastSelectedWallet", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string? LastSelectedWallet
	{
		get => _lastSelectedWallet;
		set => RaiseAndSetIfChanged(ref _lastSelectedWallet, value);
	}

	// OnDeserialized changes this default on Linux.
	[DefaultValue(false)]
	[JsonProperty(PropertyName = "RunOnSystemStartup", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool RunOnSystemStartup
	{
		get => _runOnSystemStartup;
		set => RaiseAndSetIfChanged(ref _runOnSystemStartup, value);
	}

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "HideOnClose", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool HideOnClose
	{
		get => _hideOnClose;
		set => RaiseAndSetIfChanged(ref _hideOnClose, value);
	}

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "SendAmountConversionReversed", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool SendAmountConversionReversed
	{
		get => _sendAmountConversionReversed;
		internal set => RaiseAndSetIfChanged(ref _sendAmountConversionReversed, value);
	}

	[JsonProperty(PropertyName = "WindowWidth")]
	public double? WindowWidth
	{
		get => _windowWidth;
		internal set => RaiseAndSetIfChanged(ref _windowWidth, value);
	}

	[JsonProperty(PropertyName = "WindowHeight")]
	public double? WindowHeight
	{
		get => _windowHeight;
		internal set => RaiseAndSetIfChanged(ref _windowHeight, value);
	}
}
