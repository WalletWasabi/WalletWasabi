﻿using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Disposables;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;

namespace WalletWasabi.Gui.Tabs
{
	internal class SettingsViewModel : WasabiDocumentTabViewModel
	{
		private string _network;
		private string _torHost;
		private string _torPort;
		private bool _autocopy;
		private string _autocopyText;
		private bool _useTor;
		private string _useTorText;
		private bool _isModified;
		private CompositeDisposable _disposables;

		public ReactiveCommand OpenConfigFileCommand { get; }
		public ReactiveCommand LurkingWifeModeCommand { get; }

		public SettingsViewModel() : base("Settings")
		{
			var config = new Config(Global.Config.FilePath);
			Autocopy = (bool)Global.UiConfig.Autocopy;

			this.WhenAnyValue(x => x.Network, x => x.TorHost, x => x.TorPort, x => x.UseTor).Subscribe(x => Save());

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
			if (_disposables != null)
			{
				throw new Exception("Settings was opened before it was closed.");
			}

			_disposables = new CompositeDisposable();

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(LurkingWifeMode));
				this.RaisePropertyChanged(nameof(LurkingWifeModeText));
			}).DisposeWith(_disposables);

			base.OnOpen();
		}

		public override bool OnClose()
		{
			_disposables.Dispose();
			_disposables = null;

			return base.OnClose();
		}

		public IEnumerable<string> Networks
		{
			get
			{
				return new[]{
					  "Main"
					, "TestNet"
					, "RegTest"
				};
			}
		}

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

		public bool LurkingWifeMode => Global.UiConfig.LurkingWifeMode == true;

		public string LurkingWifeModeText => Global.UiConfig.LurkingWifeMode == true ? "On" : "Off";

		private void Save()
		{
			var isValid = string.IsNullOrEmpty(ValidateTorHost()) &&
							string.IsNullOrEmpty(ValidateTorPort());
			if (!isValid) return;
			if (string.IsNullOrWhiteSpace(Network)) return;

			var config = new Config(Global.Config.FilePath);

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await config.LoadFileAsync();

				var network = NBitcoin.Network.GetNetwork(Network);
				var torHost = TorHost;
				var torSocks5Port = int.TryParse(TorPort, out var port) ? (int?)port : null;
				var useTor = UseTor;

				if (config.Network != network || config.TorHost != torHost || config.TorSocks5Port != torSocks5Port || config.UseTor != useTor)
				{
					config.Network = network;
					config.TorHost = torHost;
					config.TorSocks5Port = torSocks5Port;
					config.UseTor = useTor;

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
			if (Uri.TryCreate(torHost, UriKind.Absolute, out var uri))
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
			if (ushort.TryParse(torPort, out var port))
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
