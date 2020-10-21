using Avalonia.Input;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.CreateWallet
{
	public class ConfirmRecoveryWordsViewModel : ViewModelBase, IDisposable, IRoutableViewModel
	{
		private bool _isConfirmationFinished;

		public ConfirmRecoveryWordsViewModel(IScreen screen, List<RecoveryWord> mnemonicWords, KeyManager keyManager, Global global)
		{
			HostScreen = screen;

			ConfirmationWords = new ObservableCollection<RecoveryWord>();
			ConfirmationWords.CollectionChanged += OnConfirmationWordCollectionChanged;

			FinishCommand = ReactiveCommand.Create(() => screen.Router.NavigationStack.Clear());
			//FinishCommand = ReactiveCommand.Create(() =>
			//{
			//	global.WalletManager.AddWallet(keyManager);
			//	screen.Router.NavigationStack.Clear();
			//});
			CancelCommand = ReactiveCommand.Create(() => HostScreen.Router.NavigateAndReset.Execute(new SettingsPageViewModel(screen)));

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
		public ICommand CancelCommand { get; }
		public ICommand GoBackCommand => HostScreen.Router.NavigateBack;

		private void OnConfirmationWordCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Add)
			{
				foreach (RecoveryWord item in e.NewItems)
				{
					item.PropertyChanged += RecoveryWordOnPropertyChanged;
				}
			}
		}

		private void RecoveryWordOnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(RecoveryWord.IsConfirmed) && FocusManager.Instance.Current is { } focusManager && sender is RecoveryWord recoveryWord)
			{
				var nextToFocus = KeyboardNavigationHandler.GetNext(focusManager, NavigationDirection.Next);
				var prevToFocus = KeyboardNavigationHandler.GetNext(focusManager, NavigationDirection.Previous);

				var currentWordIndex = recoveryWord.Index;

				if (ConfirmationWords.Where(x => !x.IsConfirmed).Any(x => x.Index < currentWordIndex))
				{
					prevToFocus?.Focus();
				}
				else if (ConfirmationWords.Where(x => !x.IsConfirmed).Any(x => x.Index > currentWordIndex))
				{
					nextToFocus?.Focus();
				}
				else
				{
					IsConfirmationFinished = true;
					nextToFocus?.Focus();
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

		public void Dispose()
		{
			foreach (RecoveryWord item in ConfirmationWords)
			{
				item.PropertyChanged -= RecoveryWordOnPropertyChanged;
			}

			ConfirmationWords.CollectionChanged -= OnConfirmationWordCollectionChanged;
		}
	}
}