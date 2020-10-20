using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.CreateWallet
{
	public class ConfirmRecoveryWordsViewModel : ViewModelBase, IRoutableViewModel
	{
		private bool _isConfirmationFinished;

		public ConfirmRecoveryWordsViewModel(IScreen screen, List<RecoveryWord> mnemonicWords)
		{
			HostScreen = screen;

			ConfirmationWords = new ObservableCollection<RecoveryWord>();
			ConfirmationWords.CollectionChanged += OnConfirmationWordCollectionChanged;

			FinishCommand = ReactiveCommand.Create(() => screen.Router.NavigationStack.Clear());

			SetConfirmationWords(mnemonicWords);
		}

		public bool IsConfirmationFinished
		{
			get => _isConfirmationFinished;
			set => this.RaiseAndSetIfChanged(ref _isConfirmationFinished, value);
		}
		public string UrlPathSegment { get; }
		public IScreen HostScreen { get; }
		public ObservableCollection<RecoveryWord> ConfirmationWords { get; }
		public ICommand FinishCommand { get; }

		private void OnConfirmationWordCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Remove)
			{
				foreach (RecoveryWord item in e.OldItems)
				{
					item.PropertyChanged -= JumpFocus;
				}
			}
			else if (e.Action == NotifyCollectionChangedAction.Add)
			{
				foreach (RecoveryWord item in e.NewItems)
				{
					item.PropertyChanged += JumpFocus;
				}
			}
		}

		private void JumpFocus(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(RecoveryWord.IsConfirmed))
			{
				// This will prevent that case when the control only once can get the focus
				foreach (var item in ConfirmationWords.Where(x => !x.IsConfirmed))
				{
					item.IsFocused = false;
				}

				// Find the next control
				var nextToFocus = ConfirmationWords.FirstOrDefault(x => x.IsConfirmed == false);

				if (nextToFocus is { })
				{
					// Give the focus to the next TextBlock
					nextToFocus.IsFocused = true;
				}
				else
				{
					// All word is confirmed, give the focus to the finish button
					IsConfirmationFinished = true;
				}
			}
		}

		private void SetConfirmationWords(List<RecoveryWord> mnemonicWords)
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

				mnemonicWords[index].Reset();
				unsortedConfWords.Add(mnemonicWords[index]);
			}

			foreach (RecoveryWord item in unsortedConfWords.OrderBy(x => x.Index))
			{
				ConfirmationWords.Add(item);
			}
		}
	}
}
