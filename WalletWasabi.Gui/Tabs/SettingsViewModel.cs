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
		private string _torHost;
		private string _torPort;
		private string _localNodeHost;
		private string _localNodePort;
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
			Autocopy = Global.UiConfig?.Autocopy is true;

			this.WhenAnyValue(x => x.Network,
				x => x.TorHost, x => x.TorPort, x => x.UseTor)
				.Subscribe(x => Save());

			this.WhenAnyValue(
				x => x.LocalNodeHost, x => x.LocalNodePort)
				.Subscribe(x => Save());

			this.WhenAnyValue(
				x => x.SomePrivacyLevel, x => x.FinePrivacyLevel, x => x.StrongPrivacyLevel,
				x => x.DustThreshold)
				.Subscribe(x => Save());

			this.WhenAnyValue(x => x.Autocopy)
				.Subscribe(async x =>
			{
				Global.UiConfig.Autocopy = x;
				await Global.UiConfig.ToFileAsync();
			});

			Dispatcher.UIThread.PostLogException(async () =>
			{
				var config = new Config(Global.Config.FilePath);
				await config.LoadFileAsync();

				Network = config.Network.ToString();
				TorHost = config.TorHost;
				TorPort = config.TorSocks5Port.ToString();
				UseTor = config.UseTor;

				SomePrivacyLevel = config.PrivacyLevelSome.ToString();
				FinePrivacyLevel = config.PrivacyLevelFine.ToString();
				StrongPrivacyLevel = config.PrivacyLevelStrong.ToString();

				DustThreshold = config.DustThreshold.ToString();

				var endpoint = config.GetEndpoint();
				LocalNodeHost = endpoint.Host;
				LocalNodePort = endpoint.Port.ToString();

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

		[ValidateMethod(nameof(ValidateLocalNodeHost))]
		public string LocalNodeHost
		{
			get => _localNodeHost;
			set => this.RaiseAndSetIfChanged(ref _localNodeHost, value);
		}

		[ValidateMethod(nameof(ValidateLocalNodePort))]
		public string LocalNodePort
		{
			get => _localNodePort;
			set => this.RaiseAndSetIfChanged(ref _localNodePort, value);
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
			var isValid = string.IsNullOrEmpty(ValidateTorHost())
						&& string.IsNullOrEmpty(ValidateTorPort())
						&& string.IsNullOrEmpty(ValidateLocalNodeHost())
						&& string.IsNullOrEmpty(ValidateLocalNodePort());
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
				using (await ConfigLock.LockAsync())
				{
					await config.LoadFileAsync();

					var network = NBitcoin.Network.GetNetwork(Network);
					if (network == config.Network)
					{
						config.TorHost = TorHost;
						config.TorSocks5Port = int.TryParse(TorPort, out var port) ? port : Config.DefaultTorSock5Port;
						config.UseTor = UseTor;
						config.DustThreshold = decimal.TryParse(DustThreshold, out var threshold) ? Money.Coins(threshold) : Config.DefaultDustThreshold;
						config.PrivacyLevelSome = int.TryParse(SomePrivacyLevel, out int level) ? level : Config.DefaultPrivacyLevelSome;
						config.PrivacyLevelStrong = int.TryParse(StrongPrivacyLevel, out level) ? level : Config.DefaultPrivacyLevelStrong;
						config.PrivacyLevelFine = int.TryParse(FinePrivacyLevel, out level) ? level : Config.DefaultPrivacyLevelFine;
						config.SetEndpoint(LocalNodeHost, int.TryParse(LocalNodePort, out var p) ? new int?(p) : null);
					}
					else
					{
						config.Network = network;
						var endpoint = config.GetEndpoint();
						LocalNodeHost = endpoint.Host;
						LocalNodePort = endpoint.Port.ToString();
					}
					await config.ToFileAsync();
					IsModified = !Global.Config.AreDeepEqual(config);
				}
			});
		}

		public string ValidateTorHost()
			=> ValidateHost(TorHost, false);

		public string ValidateLocalNodeHost()
			=> ValidateHost(LocalNodeHost, true);

		public string ValidateTorPort()
			=> ValidatePort(TorPort);

		public string ValidateLocalNodePort()
			=> ValidatePort(LocalNodePort);

		public string ValidateSomePrivacyLevel()
			=> ValidatePrivacyLevel(SomePrivacyLevel);

		public string ValidateFinePrivacyLevel()
			=> ValidatePrivacyLevel(FinePrivacyLevel);

		public string ValidateStrongPrivacyLevel()
			=> ValidatePrivacyLevel(StrongPrivacyLevel);

		public string ValidatePrivacyLevel(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
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
			if (string.IsNullOrWhiteSpace(DustThreshold))
			{
				return string.Empty;
			}

			if (decimal.TryParse(DustThreshold, out var dust) && dust >= 0)
			{
				return string.Empty;
			}

			return "Invalid dust amount.";
		}

		public string ValidateHost(string host, bool p2p)
		{
			if (string.IsNullOrWhiteSpace(host))
			{
				return string.Empty;
			}
			try
			{
				if (p2p && !Config.TryNormalizeP2PHost(host, 9999, out host))
				{
					return "Invalid host";
				}
				
				var endpoint = NBitcoin.Utils.ParseEndpoint(host, 9999);
				if (endpoint.IsTor() && endpoint is DnsEndPoint dnsEndPoint)
				{
					try
					{
						var dnsHost = dnsEndPoint.Host;
						NBitcoin.DataEncoders.Encoders.Base32.DecodeData(dnsHost.Remove(dnsHost.Length - ".onion".Length));
					}
					catch
					{
						return "Invalid onion host.";
					}
				}
				if (endpoint is IPEndPoint ip &&
					ip.Address.AddressFamily == AddressFamily.InterNetworkV6 &&
					!endpoint.IsTor() && // Onioncat address would work even if the OS does not support IPv6
					!Socket.OSSupportsIPv6)
				{
					return "OS does not support IPv6 addresses.";
				}
				return string.Empty;
			}
			catch { }
			return "Invalid host.";
		}

		public string ValidatePort(string port)
		{
			if (string.IsNullOrEmpty(port))
			{
				return string.Empty;
			}

			var thePort = port.Trim();
			if (ushort.TryParse(thePort, out var p) && p > 1024)
			{
				return string.Empty;
			}

			return "Invalid port.";
		}

		private void OpenConfigFile()
		{
			IoHelpers.OpenFileInTextEditor(Global.Config.FilePath);
		}
	}
}
