using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	internal class PinPadViewModel : WasabiDocumentTabViewModel
	{
		private CompositeDisposable Disposables { get; set; }
		private string _maskedPin;

		public ReactiveCommand<Unit, Unit> SendPinCommand { get; }
		public ReactiveCommand<string, Unit> KeyPadCommand { get; }

		/*
		 * 7 8 9
		 * 4 5 6
		 * 1 2 3
		 */

		public string MaskedPin
		{
			get => _maskedPin;
			set => this.RaiseAndSetIfChanged(ref _maskedPin, value);
		}

		public PinPadViewModel(Global global) : base(global, "Pin Pad")
		{
			SendPinCommand = ReactiveCommand.Create(() =>
				{
					DialogResult = true;
					OnClose();
				},
				this.WhenAny(x => x.MaskedPin, (maskedPin) => !string.IsNullOrWhiteSpace(maskedPin.Value)));

			KeyPadCommand = ReactiveCommand.Create<string>((arg) => MaskedPin += arg);

			Observable.Merge(SendPinCommand.ThrownExceptions)
				.Merge(KeyPadCommand.ThrownExceptions)
				.Subscribe(OnException);
		}

		public override void OnOpen()
		{
			base.OnOpen();

			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");
		}

		public override bool OnClose()
		{
			Disposables.Dispose();
			Disposables = null;

			return base.OnClose();
		}

		private void OnException(Exception ex)
		{
			SetWarningMessage(ex.ToTypeMessageString());
		}

		public static async Task UnlockAsync(Global global)
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			var client = new HwiClient(global.Network);
			IEnumerable<HwiEnumerateEntry> hwiEntries = await client.EnumerateAsync(cts.Token);

			foreach (var hwiEntry in hwiEntries.Where(x => x.NeedsPinSent is true))
			{
				await UnlockAsync(global, hwiEntry);
			}
		}

		public static async Task UnlockAsync(Global global, HwiEnumerateEntry hwiEntry)
		{
			// Make sure to select back the document that was selected.
			var selectedDocument = IoC.Get<IShell>().SelectedDocument;
			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
				var client = new HwiClient(global.Network);

				await client.PromptPinAsync(hwiEntry.Model, hwiEntry.Path, cts.Token);

				PinPadViewModel pinpad = IoC.Get<IShell>().Documents.OfType<PinPadViewModel>().FirstOrDefault();
				if (pinpad is null)
				{
					pinpad = new PinPadViewModel(global);
					IoC.Get<IShell>().AddOrSelectDocument(pinpad);
				}
				var result = await pinpad.ShowDialogAsync();
				if (!(result is true))
				{
					throw new SecurityException("PIN was not provided.");
				}

				var maskedPin = pinpad.MaskedPin;

				await client.SendPinAsync(hwiEntry.Model, hwiEntry.Path, int.Parse(maskedPin), cts.Token);
			}
			finally
			{
				if (selectedDocument != null)
				{
					IoC.Get<IShell>().Select(selectedDocument);
				}
			}
		}
	}
}
