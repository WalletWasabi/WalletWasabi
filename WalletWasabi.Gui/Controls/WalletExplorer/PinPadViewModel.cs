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
using System.Text;
using WalletWasabi.Gui.ViewModels;

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

			KeyPadCommand = ReactiveCommand.Create<string>((arg) =>
			{
				MaskedPin += arg;
			});

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
	}
}
