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

namespace WalletWasabi.Gui.Tabs
{
	internal class SettingsViewModel : WasabiDocumentTabViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private Network _network;
		private string _torSocks5EndPoint;
		private string _bitcoinP2pEndPoint;
		private bool _autocopy;
		private bool _customFee;
		private bool _useTor;
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
			SomePrivacyLevel = globalConfig.PrivacyLevelSome.ToString();
			FinePrivacyLevel = globalConfig.PrivacyLevelFine.ToString();
			StrongPrivacyLevel = globalConfig.PrivacyLevelStrong.ToString();
			DustThreshold = globalConfig.DustThreshold.ToString();
			BitcoinP2pEndPoint = globalConfig.GetP2PEndpoint().ToString(defaultPort: -1);
			IsModified = false;

			this.WhenAnyValue(
				x => x.Network,
				x => x.UseTor)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => Save());

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

					SomePrivacyLevel = x.PrivacyLevelSome.ToString();
					FinePrivacyLevel = x.PrivacyLevelFine.ToString();
					StrongPrivacyLevel = x.PrivacyLevelStrong.ToString();

					DustThreshold = x.DustThreshold.ToString();

					BitcoinP2pEndPoint = x.GetP2PEndpoint().ToString(defaultPort: -1);

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
				string.IsNullOrEmpty(ValidatePrivacyLevel(SomePrivacyLevel, whiteSpaceOk: false))
				&& string.IsNullOrEmpty(ValidatePrivacyLevel(FinePrivacyLevel, whiteSpaceOk: false))
				&& string.IsNullOrEmpty(ValidatePrivacyLevel(StrongPrivacyLevel, whiteSpaceOk: false))
				&& string.IsNullOrEmpty(ValidateDustThreshold(DustThreshold, whiteSpaceOk: false))
				&& string.IsNullOrEmpty(ValidateEndPoint(TorSocks5EndPoint, Constants.DefaultTorSocksPort, whiteSpaceOk: false))
				&& string.IsNullOrEmpty(ValidateEndPoint(BitcoinP2pEndPoint, network.DefaultPort, whiteSpaceOk: false));

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

		public string ValidateSomePrivacyLevel()
			=> ValidatePrivacyLevel(SomePrivacyLevel, whiteSpaceOk: true);

		public string ValidateFinePrivacyLevel()
			=> ValidatePrivacyLevel(FinePrivacyLevel, whiteSpaceOk: true);

		public string ValidateStrongPrivacyLevel()
			=> ValidatePrivacyLevel(StrongPrivacyLevel, whiteSpaceOk: true);

		public string ValidateDustThreshold()
			=> ValidateDustThreshold(DustThreshold, whiteSpaceOk: true);

		public string ValidateTorSocks5EndPoint()
			=> ValidateEndPoint(TorSocks5EndPoint, Constants.DefaultTorSocksPort, whiteSpaceOk: true);

		public string ValidateBitcoinP2pEndPoint()
			=> ValidateEndPoint(BitcoinP2pEndPoint, Network.DefaultPort, whiteSpaceOk: true);

		public string ValidatePrivacyLevel(string value, bool whiteSpaceOk)
		{
			if (whiteSpaceOk && string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			if (uint.TryParse(value, out _))
			{
				return string.Empty;
			}

			return "Invalid privacy level.";
		}

		public string ValidateDustThreshold(string dustThreshold, bool whiteSpaceOk)
		{
			if (whiteSpaceOk && string.IsNullOrWhiteSpace(dustThreshold))
			{
				return string.Empty;
			}

			if (decimal.TryParse(dustThreshold, out var dust) && dust >= 0)
			{
				return string.Empty;
			}

			return "Invalid dust threshold.";
		}

		public string ValidateEndPoint(string endPoint, int defaultPort, bool whiteSpaceOk)
		{
			if (whiteSpaceOk && string.IsNullOrWhiteSpace(endPoint))
			{
				return string.Empty;
			}

			if (EndPointParser.TryParse(endPoint, defaultPort, out _))
			{
				return string.Empty;
			}

			return "Invalid endpoint.";
		}

		#endregion Validation
	}
}
