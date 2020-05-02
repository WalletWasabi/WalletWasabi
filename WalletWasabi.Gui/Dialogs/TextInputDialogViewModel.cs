using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Dialogs
{
	public class TextInputDialogViewModel : ModalDialogViewModelBase
	{
		private string _textInput;

		public TextInputDialogViewModel(string title, string instructions, string watermark = "", string defaultTextInput = "") : base(Guard.Correct(title), false, false)
		{
			Instructions = Guard.Correct(instructions);
			Watermark = Guard.Correct(watermark);
			TextInput = Guard.Correct(defaultTextInput);

			var canOk = this.WhenAnyValue(x => x.TextInput)
				.Select(x => !string.IsNullOrWhiteSpace(TextInput));

			OKCommand = ReactiveCommand.Create(() => Close(true), canOk);

			CancelCommand = ReactiveCommand.Create(() =>
				{
					TextInput = "";
					Close(false);
				});

			Observable
				.Merge(OKCommand.ThrownExceptions)
				.Merge(CancelCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogWarning(ex));
		}

		public new ReactiveCommand<Unit, Unit> OKCommand { get; set; }
		public new ReactiveCommand<Unit, Unit> CancelCommand { get; set; }
		public string Instructions { get; }
		public string Watermark { get; }

		public string TextInput
		{
			get => _textInput;
			set => this.RaiseAndSetIfChanged(ref _textInput, value);
		}
	}
}
