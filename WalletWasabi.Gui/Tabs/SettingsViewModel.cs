using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Gui.Tabs
{
	internal class SettingsViewModel : WasabiDocumentTabViewModel
	{
		private Network _network;
		private string _torSocks5EndPoint;
		private string _bitcoinP2pEndPoint;
		private string _localBitcoinCoreDataDir;
		private bool _autocopy;
		private bool _customFee;
		private bool _customChangeAddress;
		private bool _useTor;
		private bool _startLocalBitcoinCoreOnStartup;
		private bool _stopLocalBitcoinCoreOnShutdown;
		private bool _isModified;
		private bool _terminateTorOnExit;
		private string _somePrivacyLevel;
		private string _finePrivacyLevel;
		private string _strongPrivacyLevel;
		private string _dustThreshold;
		private string _pinBoxText;
		private ObservableAsPropertyHelper<bool> _isPinSet;
		private FeeDisplayFormat _selectedFeeDisplayFormat;

		public SettingsViewModel() : base("Settings")
		{
			Global = Locator.Current.GetService<Global>();

			this.ValidateProperty(x => x.SomePrivacyLevel, ValidateSomePrivacyLevel);
			this.ValidateProperty(x => x.FinePrivacyLevel, ValidateFinePrivacyLevel);
			this.ValidateProperty(x => x.StrongPrivacyLevel, ValidateStrongPrivacyLevel);
			this.ValidateProperty(x => x.DustThreshold, ValidateDustThreshold);
			this.ValidateProperty(x => x.TorSocks5EndPoint, ValidateTorSocks5EndPoint);
			this.ValidateProperty(x => x.BitcoinP2pEndPoint, ValidateBitcoinP2pEndPoint);

			Autocopy = Global.UiConfig.Autocopy;
			CustomFee = Global.UiConfig.IsCustomFee;
			CustomChangeAddress = Global.UiConfig.IsCustomChangeAddress;

			var config = new Config(Global.Config.FilePath);
			config.LoadOrCreateDefaultFile();

			_network = config.Network;
			_torSocks5EndPoint = config.TorSocks5EndPoint.ToString(-1);
			UseTor = config.UseTor;
			TerminateTorOnExit = config.TerminateTorOnExit;
			StartLocalBitcoinCoreOnStartup = config.StartLocalBitcoinCoreOnStartup;
			StopLocalBitcoinCoreOnShutdown = config.StopLocalBitcoinCoreOnShutdown;

			_somePrivacyLevel = config.PrivacyLevelSome.ToString();
			_finePrivacyLevel = config.PrivacyLevelFine.ToString();
			_strongPrivacyLevel = config.PrivacyLevelStrong.ToString();

			_dustThreshold = config.DustThreshold.ToString();

			_bitcoinP2pEndPoint = config.GetBitcoinP2pEndPoint().ToString(defaultPort: -1);
			_localBitcoinCoreDataDir = config.LocalBitcoinCoreDataDir;

			IsModified = !Global.Config.AreDeepEqual(config);

			this.WhenAnyValue(
				x => x.Network,
				x => x.UseTor,
				x => x.TerminateTorOnExit,
				x => x.StartLocalBitcoinCoreOnStartup,
				x => x.StopLocalBitcoinCoreOnShutdown)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(_ => Save());

			this.WhenAnyValue(x => x.Autocopy)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => Global.UiConfig.Autocopy = x);

			this.WhenAnyValue(x => x.CustomFee)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => Global.UiConfig.IsCustomFee = x);

			this.WhenAnyValue(x => x.CustomChangeAddress)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => Global.UiConfig.IsCustomChangeAddress = x);

			OpenConfigFileCommand = ReactiveCommand.CreateFromTask(OpenConfigFileAsync);

			SetClearPinCommand = ReactiveCommand.Create(() =>
			{
				var pinBoxText = PinBoxText;
				if (string.IsNullOrEmpty(pinBoxText))
				{
					NotificationHelpers.Error("Please provide a PIN.");
					return;
				}

				var trimmedPinBoxText = pinBoxText?.Trim();
				if (string.IsNullOrEmpty(trimmedPinBoxText)
					|| trimmedPinBoxText.Any(x => !char.IsDigit(x)))
				{
					NotificationHelpers.Error("Invalid PIN.");
					return;
				}

				if (trimmedPinBoxText.Length > 10)
				{
					NotificationHelpers.Error("PIN is too long.");
					return;
				}

				var uiConfigPinHash = Global.UiConfig.LockScreenPinHash;
				var enteredPinHash = HashHelpers.GenerateSha256Hash(trimmedPinBoxText);

				if (IsPinSet)
				{
					if (uiConfigPinHash != enteredPinHash)
					{
						NotificationHelpers.Error("PIN is incorrect.");
						PinBoxText = "";
						return;
					}

					Global.UiConfig.LockScreenPinHash = "";
					NotificationHelpers.Success("PIN was cleared.");
				}
				else
				{
					Global.UiConfig.LockScreenPinHash = enteredPinHash;
					NotificationHelpers.Success("PIN was changed.");
				}

				PinBoxText = "";
			});

			TextBoxLostFocusCommand = ReactiveCommand.Create(Save);

			Observable
				.Merge(OpenConfigFileCommand.ThrownExceptions)
				.Merge(SetClearPinCommand.ThrownExceptions)
				.Merge(TextBoxLostFocusCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));

			SelectedFeeDisplayFormat = Enum.IsDefined(typeof(FeeDisplayFormat), Global.UiConfig.FeeDisplayFormat)
				? (FeeDisplayFormat)Global.UiConfig.FeeDisplayFormat
				: FeeDisplayFormat.SatoshiPerByte;

			this.WhenAnyValue(x => x.SelectedFeeDisplayFormat)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => Global.UiConfig.FeeDisplayFormat = (int)x);
		}

		private bool TabOpened { get; set; }

		public bool IsPinSet => _isPinSet?.Value ?? false;

		private Global Global { get; }
		private object ConfigLock { get; } = new object();

		public ReactiveCommand<Unit, Unit> OpenConfigFileCommand { get; }
		public ReactiveCommand<Unit, Unit> SetClearPinCommand { get; }
		public ReactiveCommand<Unit, Unit> TextBoxLostFocusCommand { get; }

		public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

		public IEnumerable<Network> Networks => new[]
		{
			Network.Main,
			Network.TestNet,
			Network.RegTest
		};

		public Network Network
		{
			get => _network;
			set => this.RaiseAndSetIfChanged(ref _network, value);
		}

		public string TorSocks5EndPoint
		{
			get => _torSocks5EndPoint;
			set => this.RaiseAndSetIfChanged(ref _torSocks5EndPoint, value);
		}

		public string BitcoinP2pEndPoint
		{
			get => _bitcoinP2pEndPoint;
			set => this.RaiseAndSetIfChanged(ref _bitcoinP2pEndPoint, value);
		}

		public string LocalBitcoinCoreDataDir
		{
			get => _localBitcoinCoreDataDir;
			set => this.RaiseAndSetIfChanged(ref _localBitcoinCoreDataDir, value);
		}

		public bool IsModified
		{
			get => _isModified;
			set => this.RaiseAndSetIfChanged(ref _isModified, value);
		}

		public bool Autocopy
		{
			get => _autocopy;
			set => this.RaiseAndSetIfChanged(ref _autocopy, value);
		}

		public bool CustomFee
		{
			get => _customFee;
			set => this.RaiseAndSetIfChanged(ref _customFee, value);
		}

		public bool CustomChangeAddress
		{
			get => _customChangeAddress;
			set => this.RaiseAndSetIfChanged(ref _customChangeAddress, value);
		}

		public bool StartLocalBitcoinCoreOnStartup
		{
			get => _startLocalBitcoinCoreOnStartup;
			set => this.RaiseAndSetIfChanged(ref _startLocalBitcoinCoreOnStartup, value);
		}

		public bool StopLocalBitcoinCoreOnShutdown
		{
			get => _stopLocalBitcoinCoreOnShutdown;
			set => this.RaiseAndSetIfChanged(ref _stopLocalBitcoinCoreOnShutdown, value);
		}

		public bool UseTor
		{
			get => _useTor;
			set => this.RaiseAndSetIfChanged(ref _useTor, value);
		}

		public bool TerminateTorOnExit
		{
			get => _terminateTorOnExit;
			set => this.RaiseAndSetIfChanged(ref _terminateTorOnExit, value);
		}

		public string SomePrivacyLevel
		{
			get => _somePrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _somePrivacyLevel, value);
		}

		public string FinePrivacyLevel
		{
			get => _finePrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _finePrivacyLevel, value);
		}

		public string StrongPrivacyLevel
		{
			get => _strongPrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _strongPrivacyLevel, value);
		}

		public string DustThreshold
		{
			get => _dustThreshold;
			set => this.RaiseAndSetIfChanged(ref _dustThreshold, value);
		}

		public string PinBoxText
		{
			get => _pinBoxText;
			set => this.RaiseAndSetIfChanged(ref _pinBoxText, value);
		}

		public IEnumerable<FeeDisplayFormat> FeeDisplayFormats => Enum.GetValues(typeof(FeeDisplayFormat)).Cast<FeeDisplayFormat>();

		public FeeDisplayFormat SelectedFeeDisplayFormat
		{
			get => _selectedFeeDisplayFormat;
			set => this.RaiseAndSetIfChanged(ref _selectedFeeDisplayFormat, value);
		}

		public override void OnOpen(CompositeDisposable disposables)
		{
			try
			{
				_isPinSet = Global.UiConfig
					.WhenAnyValue(x => x.LockScreenPinHash, x => !string.IsNullOrWhiteSpace(x))
					.ToProperty(this, x => x.IsPinSet, scheduler: RxApp.MainThreadScheduler)
					.DisposeWith(disposables);
				this.RaisePropertyChanged(nameof(IsPinSet)); // Fire now otherwise the button won't update for restart.

				Global.UiConfig.WhenAnyValue(x => x.FeeDisplayFormat)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(x => SelectedFeeDisplayFormat = (FeeDisplayFormat)x)
					.DisposeWith(disposables);

				base.OnOpen(disposables);
			}
			finally
			{
				TabOpened = true;
			}
		}

		public override bool OnClose()
		{
			TabOpened = false;

			return base.OnClose();
		}

		private void Save()
		{
			// While the Tab is opening we are setting properties with loading and also LostFocus command called by Avalonia
			// Those would trigger the Save function before we load the config.
			if (!TabOpened)
			{
				return;
			}

			var network = Network;
			if (network is null)
			{
				return;
			}

			if (Validations.Any)
			{
				return;
			}

			var config = new Config(Global.Config.FilePath);

			Dispatcher.UIThread.PostLogException(() =>
			{
				lock (ConfigLock)
				{
					config.LoadFile();
					if (Network == config.Network)
					{
						if (EndPointParser.TryParse(TorSocks5EndPoint, Constants.DefaultTorSocksPort, out EndPoint? torEp))
						{
							config.TorSocks5EndPoint = torEp;
						}
						if (EndPointParser.TryParse(BitcoinP2pEndPoint, network.DefaultPort, out EndPoint? p2pEp))
						{
							config.SetBitcoinP2pEndpoint(p2pEp);
						}
						config.UseTor = UseTor;
						config.TerminateTorOnExit = TerminateTorOnExit;
						config.StartLocalBitcoinCoreOnStartup = StartLocalBitcoinCoreOnStartup;
						config.StopLocalBitcoinCoreOnShutdown = StopLocalBitcoinCoreOnShutdown;
						config.LocalBitcoinCoreDataDir = Guard.Correct(LocalBitcoinCoreDataDir);
						config.DustThreshold = decimal.TryParse(DustThreshold, out var threshold) ? Money.Coins(threshold) : Config.DefaultDustThreshold;
						config.PrivacyLevelSome = int.TryParse(SomePrivacyLevel, out int level) ? level : Config.DefaultPrivacyLevelSome;
						config.PrivacyLevelStrong = int.TryParse(StrongPrivacyLevel, out level) ? level : Config.DefaultPrivacyLevelStrong;
						config.PrivacyLevelFine = int.TryParse(FinePrivacyLevel, out level) ? level : Config.DefaultPrivacyLevelFine;
					}
					else
					{
						config.Network = Network;
						BitcoinP2pEndPoint = config.GetBitcoinP2pEndPoint().ToString(defaultPort: -1);
					}
					config.ToFile();
					IsModified = !Global.Config.AreDeepEqual(config);
				}
			});
		}

		private async Task OpenConfigFileAsync()
		{
			await FileHelpers.OpenFileInTextEditorAsync(Global.Config.FilePath);
		}

		#region Validation

		private void ValidateSomePrivacyLevel(IValidationErrors errors)
			=> ValidatePrivacyLevel(errors, SomePrivacyLevel, whiteSpaceOk: true);

		private void ValidateFinePrivacyLevel(IValidationErrors errors)
			=> ValidatePrivacyLevel(errors, FinePrivacyLevel, whiteSpaceOk: true);

		private void ValidateStrongPrivacyLevel(IValidationErrors errors)
			=> ValidatePrivacyLevel(errors, StrongPrivacyLevel, whiteSpaceOk: true);

		private void ValidateDustThreshold(IValidationErrors errors)
			=> ValidateDustThreshold(errors, DustThreshold, whiteSpaceOk: true);

		private void ValidateTorSocks5EndPoint(IValidationErrors errors)
			=> ValidateEndPoint(errors, TorSocks5EndPoint, Constants.DefaultTorSocksPort, whiteSpaceOk: true);

		private void ValidateBitcoinP2pEndPoint(IValidationErrors errors)
			=> ValidateEndPoint(errors, BitcoinP2pEndPoint, Network.DefaultPort, whiteSpaceOk: true);

		private void ValidatePrivacyLevel(IValidationErrors errors, string value, bool whiteSpaceOk)
		{
			if (!whiteSpaceOk || !string.IsNullOrWhiteSpace(value))
			{
				if (!uint.TryParse(value, out _))
				{
					errors.Add(ErrorSeverity.Error, "Invalid privacy level.");
				}
			}
		}

		private void ValidateDustThreshold(IValidationErrors errors, string dustThreshold, bool whiteSpaceOk)
		{
			if (!whiteSpaceOk || !string.IsNullOrWhiteSpace(dustThreshold))
			{
				if (!string.IsNullOrEmpty(dustThreshold) && dustThreshold.Contains(',', StringComparison.InvariantCultureIgnoreCase))
				{
					errors.Add(ErrorSeverity.Error, "Use decimal point instead of comma.");
				}

				if (!decimal.TryParse(dustThreshold, out var dust) || dust < 0)
				{
					errors.Add(ErrorSeverity.Error, "Invalid dust threshold.");
				}
			}
		}

		private void ValidateEndPoint(IValidationErrors errors, string endPoint, int defaultPort, bool whiteSpaceOk)
		{
			if (!whiteSpaceOk || !string.IsNullOrWhiteSpace(endPoint))
			{
				if (!EndPointParser.TryParse(endPoint, defaultPort, out _))
				{
					errors.Add(ErrorSeverity.Error, "Invalid endpoint.");
				}
			}
		}

		#endregion Validation
	}
}
