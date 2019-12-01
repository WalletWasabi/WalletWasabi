using Avalonia.Threading;
using NBitcoin;
using Nito.AsyncEx;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.RegularExpressions;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs
{
	internal class SettingsViewModel : WasabiDocumentTabViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private Network _network;
		private string _torSocks5EndPoint;
		private string _bitcoinP2pEndPoint;
		private string _localBitcoinCoreDataDir;
		private bool _autocopy;
		private bool _customFee;
		private bool _useTor;
		private bool _startLocalBitcoinCoreOnStartup;
		private bool _stopLocalBitcoinCoreOnShutdown;
		private bool _isModified;
		private string _somePrivacyLevel;
		private string _finePrivacyLevel;
		private string _strongPrivacyLevel;
		private string _dustThreshold;
		private string _pinBoxText;
		private string _pinWarningMessage;

		private ObservableAsPropertyHelper<bool> _isPinSet;

		public bool IsPinSet => _isPinSet?.Value ?? false;
		private AsyncLock ConfigLock { get; } = new AsyncLock();

		public ReactiveCommand<Unit, Unit> OpenConfigFileCommand { get; }
		public ReactiveCommand<Unit, Unit> LurkingWifeModeCommand { get; }
		public ReactiveCommand<Unit, Unit> SetClearPinCommand { get; }
		public ReactiveCommand<Unit, Unit> TextBoxLostFocusCommand { get; }

		public SettingsViewModel(Global global) : base(global, "Settings")
		{
			Autocopy = Global.UiConfig?.Autocopy is true;
			CustomFee = Global.UiConfig?.IsCustomFee is true;

			// Use global config's data as default filler until the real data is filled out by the loading of the config onopen.
			var globalConfig = Global.Config;
			Network = globalConfig.Network;
			TorSocks5EndPoint = globalConfig.TorSocks5EndPoint.ToString(-1);
			UseTor = globalConfig.UseTor;
			StartLocalBitcoinCoreOnStartup = globalConfig.StartLocalBitcoinCoreOnStartup;
			StopLocalBitcoinCoreOnShutdown = globalConfig.StopLocalBitcoinCoreOnShutdown;
			SomePrivacyLevel = globalConfig.PrivacyLevelSome.ToString();
			FinePrivacyLevel = globalConfig.PrivacyLevelFine.ToString();
			StrongPrivacyLevel = globalConfig.PrivacyLevelStrong.ToString();
			DustThreshold = globalConfig.DustThreshold.ToString();
			BitcoinP2pEndPoint = globalConfig.GetP2PEndpoint().ToString(defaultPort: -1);
			LocalBitcoinCoreDataDir = globalConfig.LocalBitcoinCoreDataDir;
			IsModified = false;

			this.WhenAnyValue(
				x => x.Network,
				x => x.UseTor,
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

			OpenConfigFileCommand = ReactiveCommand.Create(OpenConfigFile);

			LurkingWifeModeCommand = ReactiveCommand.CreateFromTask(async () =>
				{
					Global.UiConfig.LurkingWifeMode = !LurkingWifeMode;
					await Global.UiConfig.ToFileAsync();
				});

			SetClearPinCommand = ReactiveCommand.Create(() =>
				{
					var pinBoxText = PinBoxText?.Trim();
					if (string.IsNullOrWhiteSpace(pinBoxText))
					{
						PinWarningMessage = "Please provide PIN.";
						return;
					}

					if (pinBoxText.Length > 10)
					{
						PinWarningMessage = "PIN too long.";
						return;
					}

					if (pinBoxText.Any(x => !char.IsDigit(x)))
					{
						PinWarningMessage = "Invalid PIN.";
						return;
					}

					var uiConfigPinHash = Global.UiConfig.LockScreenPinHash;
					var enteredPinHash = HashHelpers.GenerateSha256Hash(pinBoxText);

					if (IsPinSet)
					{
						if (uiConfigPinHash != enteredPinHash)
						{
							PinWarningMessage = "Wrong PIN.";
							PinBoxText = string.Empty;
							return;
						}

						Global.UiConfig.LockScreenPinHash = string.Empty;
					}
					else
					{
						Global.UiConfig.LockScreenPinHash = enteredPinHash;
					}

					PinBoxText = string.Empty;
					PinWarningMessage = string.Empty;
				});

			TextBoxLostFocusCommand = ReactiveCommand.Create(Save);

			Observable
				.Merge(OpenConfigFileCommand.ThrownExceptions)
				.Merge(LurkingWifeModeCommand.ThrownExceptions)
				.Merge(SetClearPinCommand.ThrownExceptions)
				.Merge(TextBoxLostFocusCommand.ThrownExceptions)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public override void OnOpen()
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			Config.LoadOrCreateDefaultFileAsync(Global.Config.FilePath)
				.ToObservable(RxApp.TaskpoolScheduler)
				.Take(1)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					Network = x.Network;
					TorSocks5EndPoint = x.TorSocks5EndPoint.ToString(-1);
					UseTor = x.UseTor;
					StartLocalBitcoinCoreOnStartup = x.StartLocalBitcoinCoreOnStartup;
					StopLocalBitcoinCoreOnShutdown = x.StopLocalBitcoinCoreOnShutdown;

					SomePrivacyLevel = x.PrivacyLevelSome.ToString();
					FinePrivacyLevel = x.PrivacyLevelFine.ToString();
					StrongPrivacyLevel = x.PrivacyLevelStrong.ToString();

					DustThreshold = x.DustThreshold.ToString();

					BitcoinP2pEndPoint = x.GetP2PEndpoint().ToString(defaultPort: -1);
					LocalBitcoinCoreDataDir = x.LocalBitcoinCoreDataDir;

					IsModified = !Global.Config.AreDeepEqual(x);
				})
				.DisposeWith(Disposables);

			Global.UiConfig
				.WhenAnyValue(x => x.LurkingWifeMode)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(LurkingWifeMode)))
				.DisposeWith(Disposables);

			_isPinSet = Global.UiConfig.WhenAnyValue(x => x.LockScreenPinHash, x => !string.IsNullOrWhiteSpace(x))
				.ToProperty(this, x => x.IsPinSet, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			Global.UiConfig.WhenAnyValue(x => x.LockScreenPinHash, x => x.Autocopy, x => x.IsCustomFee)
				.Throttle(TimeSpan.FromSeconds(1))
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(async _ => await Global.UiConfig.ToFileAsync())
				.DisposeWith(Disposables);

			base.OnOpen();
		}

		public override bool OnClose()
		{
			Disposables?.Dispose();
			Disposables = null;

			return base.OnClose();
		}

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

		[ValidateMethod(nameof(ValidateTorSocks5EndPoint))]
		public string TorSocks5EndPoint
		{
			get => _torSocks5EndPoint;
			set => this.RaiseAndSetIfChanged(ref _torSocks5EndPoint, value);
		}

		[ValidateMethod(nameof(ValidateBitcoinP2pEndPoint))]
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

		[ValidateMethod(nameof(ValidateSomePrivacyLevel))]
		public string SomePrivacyLevel
		{
			get => _somePrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _somePrivacyLevel, value);
		}

		[ValidateMethod(nameof(ValidateFinePrivacyLevel))]
		public string FinePrivacyLevel
		{
			get => _finePrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _finePrivacyLevel, value);
		}

		[ValidateMethod(nameof(ValidateStrongPrivacyLevel))]
		public string StrongPrivacyLevel
		{
			get => _strongPrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _strongPrivacyLevel, value);
		}

		[ValidateMethod(nameof(ValidateDustThreshold))]
		public string DustThreshold
		{
			get => _dustThreshold;
			set => this.RaiseAndSetIfChanged(ref _dustThreshold, value);
		}

		public bool LurkingWifeMode => Global.UiConfig.LurkingWifeMode is true;

		public string PinBoxText
		{
			get => _pinBoxText;
			set => this.RaiseAndSetIfChanged(ref _pinBoxText, value);
		}

		public string PinWarningMessage
		{
			get => _pinWarningMessage;
			set => this.RaiseAndSetIfChanged(ref _pinWarningMessage, value);
		}

		private void Save()
		{
			var network = Network;
			if (network is null)
			{
				return;
			}

			var isValid =
				!ValidatePrivacyLevel(SomePrivacyLevel, whiteSpaceOk: false).HasErrors
				&& !ValidatePrivacyLevel(FinePrivacyLevel, whiteSpaceOk: false).HasErrors
				&& !ValidatePrivacyLevel(StrongPrivacyLevel, whiteSpaceOk: false).HasErrors
				&& !ValidateDustThreshold(DustThreshold, whiteSpaceOk: false).HasErrors
				&& !ValidateEndPoint(TorSocks5EndPoint, Constants.DefaultTorSocksPort, whiteSpaceOk: false).HasErrors
				&& !ValidateEndPoint(BitcoinP2pEndPoint, network.DefaultPort, whiteSpaceOk: false).HasErrors;

			if (!isValid)
			{
				return;
			}

			var config = new Config(Global.Config.FilePath);

			Dispatcher.UIThread.PostLogException(async () =>
			{
				using (await ConfigLock.LockAsync())
				{
					await config.LoadFileAsync();
					if (Network == config.Network)
					{
						if (EndPointParser.TryParse(TorSocks5EndPoint, Constants.DefaultTorSocksPort, out EndPoint torEp))
						{
							config.TorSocks5EndPoint = torEp;
						}
						if (EndPointParser.TryParse(BitcoinP2pEndPoint, network.DefaultPort, out EndPoint p2pEp))
						{
							config.SetP2PEndpoint(p2pEp);
						}
						config.UseTor = UseTor;
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
						BitcoinP2pEndPoint = config.GetP2PEndpoint().ToString(defaultPort: -1);
					}
					await config.ToFileAsync();
					IsModified = !Global.Config.AreDeepEqual(config);
				}
			});
		}

		private void OpenConfigFile()
		{
			IoHelpers.OpenFileInTextEditor(Global.Config.FilePath);
		}

		#region Validation

		public ErrorDescriptors ValidateSomePrivacyLevel()
			=> ValidatePrivacyLevel(SomePrivacyLevel, whiteSpaceOk: true);

		public ErrorDescriptors ValidateFinePrivacyLevel()
			=> ValidatePrivacyLevel(FinePrivacyLevel, whiteSpaceOk: true);

		public ErrorDescriptors ValidateStrongPrivacyLevel()
			=> ValidatePrivacyLevel(StrongPrivacyLevel, whiteSpaceOk: true);

		public ErrorDescriptors ValidateDustThreshold()
			=> ValidateDustThreshold(DustThreshold, whiteSpaceOk: true);

		public ErrorDescriptors ValidateTorSocks5EndPoint()
			=> ValidateEndPoint(TorSocks5EndPoint, Constants.DefaultTorSocksPort, whiteSpaceOk: true);

		public ErrorDescriptors ValidateBitcoinP2pEndPoint()
			=> ValidateEndPoint(BitcoinP2pEndPoint, Network.DefaultPort, whiteSpaceOk: true);

		public ErrorDescriptors ValidatePrivacyLevel(string value, bool whiteSpaceOk)
		{
			if (whiteSpaceOk && string.IsNullOrWhiteSpace(value))
			{
				return ErrorDescriptors.Empty;
			}

			if (uint.TryParse(value, out _))
			{
				return ErrorDescriptors.Empty;
			}

			return new ErrorDescriptors(new ErrorDescriptor(ErrorSeverity.Error, "Invalid privacy level."));
		}

		public ErrorDescriptors ValidateDustThreshold(string dustThreshold, bool whiteSpaceOk)
		{
			if (whiteSpaceOk && string.IsNullOrWhiteSpace(dustThreshold))
			{
				return ErrorDescriptors.Empty;
			}

			if (!string.IsNullOrEmpty(dustThreshold) && dustThreshold.Contains(',', StringComparison.InvariantCultureIgnoreCase))
			{
				return new ErrorDescriptors(new ErrorDescriptor(ErrorSeverity.Error, "Use decimal point instead of comma."));
			}

			if (decimal.TryParse(dustThreshold, out var dust) && dust >= 0)
			{
				return ErrorDescriptors.Empty;
			}

			return new ErrorDescriptors(new ErrorDescriptor(ErrorSeverity.Error, "Invalid dust threshold."));
		}

		public ErrorDescriptors ValidateEndPoint(string endPoint, int defaultPort, bool whiteSpaceOk)
		{
			if (whiteSpaceOk && string.IsNullOrWhiteSpace(endPoint))
			{
				return ErrorDescriptors.Empty;
			}

			if (EndPointParser.TryParse(endPoint, defaultPort, out _))
			{
				return ErrorDescriptors.Empty;
			}

			return new ErrorDescriptors(new ErrorDescriptor(ErrorSeverity.Error, "Invalid endpoint."));
		}

		#endregion Validation
	}
}
