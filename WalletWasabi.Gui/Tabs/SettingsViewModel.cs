using Avalonia.Threading;
using NBitcoin;
using Nito.AsyncEx;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;

namespace WalletWasabi.Gui.Tabs
{
	internal class SettingsViewModel : WasabiDocumentTabViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private string _network;
		private string _torSocks5EndPoint;
		private string _bitcoinP2pEndPoint;
		private bool _autocopy;
		private bool _useTor;
		private bool _isModified;
		private string _somePrivacyLevel;
		private string _finePrivacyLevel;
		private string _strongPrivacyLevel;
		private string _dustThreshold;
		private AsyncLock ConfigLock { get; } = new AsyncLock();

		public ReactiveCommand<Unit, Unit> OpenConfigFileCommand { get; }
		public ReactiveCommand<Unit, Unit> LurkingWifeModeCommand { get; }

		public SettingsViewModel(Global global) : base(global, "Settings")
		{
			var config = new Config(Global.Config.FilePath);
			Autocopy = Global.UiConfig?.Autocopy is true;

			this.WhenAnyValue(x => x.Network)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async _ =>
				{
					await config.LoadFileAsync();

					var configBitcoinP2pEndPoint = Network == NBitcoin.Network.Main.Name
						? config.MainNetBitcoinP2pEndPoint
						: (Network == NBitcoin.Network.TestNet.Name
							? config.TestNetBitcoinP2pEndPoint
							: config.RegTestBitcoinP2pEndPoint);

					BitcoinP2pEndPoint = configBitcoinP2pEndPoint.ToString(defaultPort: -1);
				});

			this.WhenAnyValue(
				x => x.Network,
				x => x.BitcoinP2pEndPoint,
				x => x.DustThreshold)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => Save());

			this.WhenAnyValue(
				x => x.UseTor,
				x => x.TorSocks5EndPoint)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => Save());

			this.WhenAnyValue(
				x => x.SomePrivacyLevel,
				x => x.FinePrivacyLevel,
				x => x.StrongPrivacyLevel,
				x => x.DustThreshold)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x => Save());

			this.WhenAnyValue(x => x.Autocopy)
				.Subscribe(async x =>
			{
				Global.UiConfig.Autocopy = x;
				await Global.UiConfig.ToFileAsync();
			});

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await config.LoadFileAsync();

				Network = config.Network.ToString();
				TorSocks5EndPoint = config.TorSocks5EndPoint.ToString(-1);
				UseTor = config.UseTor.Value;

				SomePrivacyLevel = config.PrivacyLevelSome.ToString();
				FinePrivacyLevel = config.PrivacyLevelFine.ToString();
				StrongPrivacyLevel = config.PrivacyLevelStrong.ToString();

				DustThreshold = config.DustThreshold.ToString();

				IsModified = await Global.Config.CheckFileChangeAsync();
			});

			OpenConfigFileCommand = ReactiveCommand.Create(OpenConfigFile);

			LurkingWifeModeCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				Global.UiConfig.LurkingWifeMode = !LurkingWifeMode;
				await Global.UiConfig.ToFileAsync();
			});
		}

		public override void OnOpen()
		{
			if (Disposables != null)
			{
				throw new Exception("Settings was opened before it was closed.");
			}

			Disposables = new CompositeDisposable();

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(LurkingWifeMode));
			}).DisposeWith(Disposables);

			base.OnOpen();
		}

		public override bool OnClose()
		{
			Disposables?.Dispose();
			Disposables = null;

			return base.OnClose();
		}

		public IEnumerable<string> Networks => new[]
		{
			"Main",
			"TestNet",
			"RegTest"
		};

		public string Network
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

		private void Save()
		{
			var isValid =
				string.IsNullOrEmpty(ValidatePrivacyLevel(SomePrivacyLevel, whiteSpaceOk: false))
				&& string.IsNullOrEmpty(ValidatePrivacyLevel(FinePrivacyLevel, whiteSpaceOk: false))
				&& string.IsNullOrEmpty(ValidatePrivacyLevel(StrongPrivacyLevel, whiteSpaceOk: false))
				&& string.IsNullOrEmpty(ValidateDustThreshold(DustThreshold, whiteSpaceOk: false))
				&& string.IsNullOrEmpty(ValidateEndPoint(TorSocks5EndPoint, whiteSpaceOk: false))
				&& string.IsNullOrEmpty(ValidateEndPoint(BitcoinP2pEndPoint, whiteSpaceOk: false));

			if (!isValid)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(Network))
			{
				return;
			}

			var config = new Config(Global.Config.FilePath);

			Dispatcher.UIThread.PostLogException(async () =>
			{
				using (await ConfigLock.LockAsync())
				{
					await config.LoadFileAsync();

					var network = NBitcoin.Network.GetNetwork(Network);
					var torSocks5EndPoint = EndPointParser.TryParse(TorSocks5EndPoint, -1, out EndPoint torEp) ? torEp : null;
					var bitcoinP2pEndPoint = EndPointParser.TryParse(BitcoinP2pEndPoint, -1, out EndPoint p2pEp) ? p2pEp : null;
					var useTor = UseTor;
					var somePrivacyLevel = int.TryParse(SomePrivacyLevel, out int level) ? (int?)level : null;
					var finePrivacyLevel = int.TryParse(FinePrivacyLevel, out level) ? (int?)level : null;
					var strongPrivacyLevel = int.TryParse(StrongPrivacyLevel, out level) ? (int?)level : null;
					var dustThreshold = decimal.TryParse(DustThreshold, out var threshold) ? (decimal?)threshold : null;

					var configBitcoinP2pEndPoint = network == NBitcoin.Network.Main
						? config.MainNetBitcoinP2pEndPoint
						: (network == NBitcoin.Network.TestNet
							? config.TestNetBitcoinP2pEndPoint
							: config.RegTestBitcoinP2pEndPoint);

					if (config.Network != network
						|| config.TorSocks5EndPoint != torSocks5EndPoint
						|| config.UseTor != useTor
						|| config.PrivacyLevelSome != somePrivacyLevel
						|| config.PrivacyLevelFine != finePrivacyLevel
						|| config.PrivacyLevelStrong != strongPrivacyLevel
						|| config.DustThreshold.ToUnit(MoneyUnit.BTC) != dustThreshold
						|| configBitcoinP2pEndPoint != bitcoinP2pEndPoint
					)
					{
						config.Network = network;
						config.TorSocks5EndPoint = torSocks5EndPoint;
						config.UseTor = useTor;
						config.PrivacyLevelSome = somePrivacyLevel;
						config.PrivacyLevelFine = finePrivacyLevel;
						config.PrivacyLevelStrong = strongPrivacyLevel;
						config.DustThreshold = Money.Coins(dustThreshold.Value);

						switch (network.Name)
						{
							case "Main":
								config.MainNetBitcoinP2pEndPoint = bitcoinP2pEndPoint;
								break;

							case "TestNet":
								config.TestNetBitcoinP2pEndPoint = bitcoinP2pEndPoint;
								break;

							case "RegTest":
								config.RegTestBitcoinP2pEndPoint = bitcoinP2pEndPoint;
								break;
						}

						await config.ToFileAsync();
					}

					IsModified = await Global.Config.CheckFileChangeAsync();
				}
			});
		}

		public string ValidateSomePrivacyLevel()
			=> ValidatePrivacyLevel(SomePrivacyLevel, whiteSpaceOk: true);

		public string ValidateFinePrivacyLevel()
			=> ValidatePrivacyLevel(FinePrivacyLevel, whiteSpaceOk: true);

		public string ValidateStrongPrivacyLevel()
			=> ValidatePrivacyLevel(StrongPrivacyLevel, whiteSpaceOk: true);

		public string ValidateDustThreshold()
			=> ValidateDustThreshold(DustThreshold, whiteSpaceOk: true);

		public string ValidateTorSocks5EndPoint()
			=> ValidateEndPoint(TorSocks5EndPoint, whiteSpaceOk: true);

		public string ValidateBitcoinP2pEndPoint()
			=> ValidateEndPoint(BitcoinP2pEndPoint, whiteSpaceOk: true);

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

		public string ValidateEndPoint(string endPoint, bool whiteSpaceOk)
		{
			if (whiteSpaceOk && string.IsNullOrWhiteSpace(endPoint))
			{
				return string.Empty;
			}

			if (EndPointParser.TryParse(endPoint, -1, out _))
			{
				return string.Empty;
			}

			return "Invalid endpoint.";
		}

		private void OpenConfigFile()
		{
			IoHelpers.OpenFileInTextEditor(Global.Config.FilePath);
		}
	}
}
