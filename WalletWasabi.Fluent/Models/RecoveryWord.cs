using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models
{
	public class RecoveryWord : ViewModelBase
	{
		private string _input;

		public RecoveryWord(int index, string word)
		{
			Index = index;
			Word = word;

			this.ValidateProperty(x => x.Input, ValidateWord);
		}

		public string Input
		{
			get => _input;
			set => this.RaiseAndSetIfChanged(ref _input, value);
		}

		public int Index { get; }
		public string Word { get; }

		private void ValidateWord(IValidationErrors errors)
		{
			if (string.IsNullOrWhiteSpace(Input))
			{
				return;
			}

			if (Input == Word)
			{
				return;
			}

			errors.Add(ErrorSeverity.Error, $"The input does not match to to the recovery word {Index}.");
		}
	}
}
