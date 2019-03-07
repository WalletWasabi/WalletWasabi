using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using Avalonia;
using System;
using System.Collections.Generic;
using Avalonia.Threading;
using WalletWasabi.Gui.ViewModels.Validation;
using System.Net;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Tabs
{
	internal class SettingsViewModel : WasabiDocumentTabViewModel, IDisposable
	{
		private CompositeDisposable Disposables { get; }

		private string _network;
		private string _torHost;
		private string _torPort;
		private bool _autocopy;
		private string _autocopyText;
		private bool _useTor;
		private string _useTorText;

		private bool _isModified;

		public SettingsViewModel() : base("Settings")
		{
			Disposables = new CompositeDisposable();

			var config = new Config(Global.Config.FilePath);
			Autocopy = (bool)Global.UiConfig.Autocopy;

			this.WhenAnyValue(x => x.Network, x => x.TorHost, x => x.TorPort, x => x.UseTor).Subscribe(x => Save()).DisposeWith(Disposables);

			this.WhenAnyValue(x => x.Autocopy).Subscribe(x =>
			{
				Dispatcher.UIThread.PostLogException(async () =>
				{
					Global.UiConfig.Autocopy = x;
					await Global.UiConfig.ToFileAsync();

					AutocopyText = x ? "On" : "Off";
				});
			}).DisposeWith(Disposables);

			this.WhenAnyValue(x => x.UseTor).Subscribe(x =>
			{
				UseTorText = x ? "On" : "Off";
			}).DisposeWith(Disposables);

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await config.LoadFileAsync();

				Network = config.Network.ToString();
				TorHost = config.TorHost;
				TorPort = config.TorSocks5Port.ToString();
				UseTor = config.UseTor.Value;

				IsModified = await Global.Config.CheckFileChangeAsync();
			});

			OpenConfigFileCommand = ReactiveCommand.Create(OpenConfigFile).DisposeWith(Disposables);
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

		public ReactiveCommand OpenConfigFileCommand { get; }

		private void OpenConfigFile()
		{
			IoHelpers.OpenFileInTextEditor(Global.Config.FilePath);
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
