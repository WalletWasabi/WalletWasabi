using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Dialogs
{
	public class TextInputDialogViewModel : ModalDialogViewModelBase
	{
		private string _textInput;
		public new ReactiveCommand<Unit, Unit> OKCommand { get; set; }
		public new ReactiveCommand<Unit, Unit> CancelCommand { get; set; }
		public string Instructions { get; }
		public string Watermark { get; }

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

			OKCommand.ThrownExceptions.Subscribe(ex => Logging.Logger.LogWarning(ex));
			CancelCommand.ThrownExceptions.Subscribe(ex => Logging.Logger.LogWarning(ex));
		}

		public string TextInput
		{
			get => _textInput;
			set => this.RaiseAndSetIfChanged(ref _textInput, value);
		}
	}
}
