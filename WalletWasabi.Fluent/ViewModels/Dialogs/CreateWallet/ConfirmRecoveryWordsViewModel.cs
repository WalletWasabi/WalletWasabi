using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.CreateWallet
{
	public class ConfirmRecoveryWordsViewModel : ViewModelBase, IRoutableViewModel
	{
		public ConfirmRecoveryWordsViewModel(IScreen screen, List<string> mnemonicWords)
		{
			HostScreen = screen;
			ConfirmationWords = new List<RecoveryWord>();

			SetConfirmationWords(mnemonicWords);
		}

		public string UrlPathSegment { get; }
		public IScreen HostScreen { get; }
		public List<RecoveryWord> ConfirmationWords { get; }

		private void SetConfirmationWords(List<string> mnemonicWords)
		{
			var random = new Random();
			var unsortedConfWords = new List<RecoveryWord>();

			for (int i = 0; i < 4; i++)
			{
				int index;
				while (true)
				{
					index = random.Next(0, 12);

					if (!unsortedConfWords.Any(x => x.Index == index + 1))
					{
						break;
					}
				}

				unsortedConfWords.Add(new RecoveryWord(index + 1, mnemonicWords[index]));
			}

			ConfirmationWords.AddRange(unsortedConfWords.OrderBy(x => x.Index));
		}
	}
}
