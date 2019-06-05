using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;

namespace WalletWasabi.Gui.Tabs
{
	internal class SettingsViewModel : WasabiDocumentTabViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private string _network;
		private string _torHost;
		private string _torPort;
		private bool _autocopy;
		private string _autocopyText;
		private bool _useTor;
		private string _useTorText;
		private bool _isModified;

		private string _somePrivacyLevel;
		private string _finePrivacyLevel;
		private string _strongPrivacyLevel;
		private string _dustThreshold;

		public ReactiveCommand<Unit, Unit> OpenConfigFileCommand { get; }
		public ReactiveCommand<Unit, Unit> LurkingWifeModeCommand { get; }

		public SettingsViewModel() : base("Settings")
		{
			var config = new Config(Global.Config.FilePath);
			Autocopy = Global.UiConfig?.Autocopy is true;

			this.WhenAnyValue(x => x.Network, 
				x => x.TorHost, x => x.TorPort, x => x.UseTor)
				.Subscribe(x => Save());

			this.WhenAnyValue(
				x => x.SomePrivacyLevel, x => x.FinePrivacyLevel, x => x.StrongPrivacyLevel,
				x=> x.DustThreshold)
				.Subscribe(x => Save());

			this.WhenAnyValue(x => x.Autocopy).Subscribe(x =>
			{
				Dispatcher.UIThread.PostLogException(async () =>
				{
					Global.UiConfig.Autocopy = x;
					await Global.UiConfig.ToFileAsync();

					AutocopyText = x ? "On" : "Off";
				});
			});

			this.WhenAnyValue(x => x.UseTor).Subscribe(x =>
			{
				UseTorText = x ? "On" : "Off";
			});

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await config.LoadFileAsync();

				Network = config.Network.ToString();
				TorHost = config.TorHost;
				TorPort = config.TorSocks5Port.ToString();
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
				this.RaisePropertyChanged(nameof(LurkingWifeModeText));
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

		[ValidateMethod(nameof(ValidateTorHost))]
		public string TorHost
		{
			get => _torHost;
			set => this.RaiseAndSetIfChanged(ref _torHost, value);
		}

		[ValidateMethod(nameof(ValidateTorPort))]
		public string TorPort
		{
			get => _torPort;
			set => this.RaiseAndSetIfChanged(ref _torPort, value);
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

		public string AutocopyText
		{
			get => _autocopyText;
			set => this.RaiseAndSetIfChanged(ref _autocopyText, value);
		}

		public bool UseTor
		{
			get => _useTor;
			set => this.RaiseAndSetIfChanged(ref _useTor, value);
		}

		public string UseTorText
		{
			get => _useTorText;
			set => this.RaiseAndSetIfChanged(ref _useTorText, value);
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

		public string LurkingWifeModeText => Global.UiConfig.LurkingWifeMode is true ? "On" : "Off";

		private void Save()
		{
			var isValid = string.IsNullOrEmpty(ValidateTorHost()) &&
							string.IsNullOrEmpty(ValidateTorPort());
			if (!isValid)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(Network)
				|| string.IsNullOrWhiteSpace(SomePrivacyLevel)
				|| string.IsNullOrWhiteSpace(FinePrivacyLevel)
				|| string.IsNullOrWhiteSpace(StrongPrivacyLevel)
				|| string.IsNullOrWhiteSpace(DustThreshold))
			{
				return;
			}

			var config = new Config(Global.Config.FilePath);

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await config.LoadFileAsync();

				var network = NBitcoin.Network.GetNetwork(Network);
				var torHost = TorHost;
				var torSocks5Port = int.TryParse(TorPort, out var port) ? (int?)port : null;
				var useTor = UseTor;
				int level;
				var somePrivacyLevel  = int.TryParse(SomePrivacyLevel,   out level) ? (int?)level : null;
				var finePrivacyLevel  = int.TryParse(FinePrivacyLevel,   out level) ? (int?)level : null;
				var strongPrivacyLevel= int.TryParse(StrongPrivacyLevel, out level) ? (int?)level : null;
				var dustThreshold= decimal.TryParse(DustThreshold, out var threshold) ? (decimal?)threshold : null;

				if (config.Network != network 
					|| config.TorHost != torHost 
					|| config.TorSocks5Port != torSocks5Port 
					|| config.UseTor != useTor
					|| config.PrivacyLevelSome != somePrivacyLevel
					|| config.PrivacyLevelFine != finePrivacyLevel
					|| config.PrivacyLevelStrong != strongPrivacyLevel
					|| config.DustThreshold.ToUnit(NBitcoin.MoneyUnit.BTC) != dustThreshold)
				{
					config.Network = network;
					config.TorHost = torHost;
					config.TorSocks5Port = torSocks5Port;
					config.UseTor = useTor;
					config.PrivacyLevelSome = somePrivacyLevel;
					config.PrivacyLevelFine = finePrivacyLevel;
					config.PrivacyLevelStrong= strongPrivacyLevel;
					config.DustThreshold = Money.Coins(dustThreshold.Value);

					await config.ToFileAsync();

					IsModified = await Global.Config.CheckFileChangeAsync();
				}
			});
		}

		public string ValidateTorHost()
		{
			if (string.IsNullOrWhiteSpace(TorHost))
			{
				return string.Empty;
			}

			var torHost = TorHost.Trim();
			if (Uri.TryCreate(torHost, UriKind.Absolute, out _))
			{
				return string.Empty;
			}
			if (IPAddress.TryParse(torHost, out var ip))
			{
				if (ip.AddressFamily == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
				{
					return "OS does not support IPv6 addresses.";
				}
				return string.Empty;
			}

			return "Invalid host.";
		}

		public string ValidateTorPort()
		{
			if (string.IsNullOrEmpty(TorPort))
			{
				return string.Empty;
			}

			var torPort = TorPort.Trim();
			if (ushort.TryParse(torPort, out _))
			{
				return string.Empty;
			}

			return "Invalid port.";
		}

		public string ValidateSomePrivacyLevel()
			=> ValidatePrivacyLevel(SomePrivacyLevel);
		public string ValidateFinePrivacyLevel()
			=> ValidatePrivacyLevel(FinePrivacyLevel);
		public string ValidateStrongPrivacyLevel()
			=> ValidatePrivacyLevel(StrongPrivacyLevel);

		public string ValidatePrivacyLevel(string value)
		{
			if(string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			if (uint.TryParse(value, out _))
			{
				return string.Empty;
			}

			return "Invalid privacy level.";
		}

		public string ValidateDustThreshold()
		{
			if(string.IsNullOrWhiteSpace(DustThreshold))
			{
				return string.Empty;
			}

			if (decimal.TryParse(DustThreshold, out _))
			{
				return string.Empty;
			}

			return "Invalid dust amount.";
		}

		private void OpenConfigFile()
		{
			IoHelpers.OpenFileInTextEditor(Global.Config.FilePath);
		}
	}
}
