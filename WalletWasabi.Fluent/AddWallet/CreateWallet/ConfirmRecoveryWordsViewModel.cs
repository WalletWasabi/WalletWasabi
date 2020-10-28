using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.AddWallet.CreateWallet
{
	public class ConfirmRecoveryWordsViewModel : ViewModelBase, IRoutableViewModel
	{
		private ReadOnlyObservableCollection<RecoveryWord> _confirmationWords;
		private SourceList<RecoveryWord> _confirmationWordsSourceList;

		public ConfirmRecoveryWordsViewModel(IScreen screen, List<RecoveryWord> mnemonicWords, KeyManager keyManager, Global global)
		{
			HostScreen = screen;

			_confirmationWordsSourceList = new SourceList<RecoveryWord>();

			FinishCommand = ReactiveCommand.Create(
				() =>
				{
					global.WalletManager.AddWallet(keyManager);
					screen.Router.NavigationStack.Clear();
				},
				_confirmationWordsSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.WhenValueChanged(x => x.IsConfirmed)
				.Select(x => !_confirmationWordsSourceList.Items.Any(x => !x.IsConfirmed)));

			_confirmationWordsSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.OnItemAdded(x => x.Reset())
				.Sort(SortExpressionComparer<RecoveryWord>.Ascending(x => x.Index))
				.Bind(out _confirmationWords)
				.Subscribe();

			SelectRandomConfirmationWords(mnemonicWords);
		}

		public string UrlPathSegment { get; } = "";
		public IScreen HostScreen { get; }

		public ReadOnlyObservableCollection<RecoveryWord> ConfirmationWords => _confirmationWords;

		public ICommand FinishCommand { get; }
		public ICommand CancelCommand { get; }
		public ICommand GoBackCommand => HostScreen.Router.NavigateBack;

		private void SelectRandomConfirmationWords(List<RecoveryWord> mnemonicWords)
		{
			var random = new Random();

			while (_confirmationWordsSourceList.Count != 4)
			{
				var word = mnemonicWords[random.Next(0, 12)];

				if (!_confirmationWordsSourceList.Items.Contains(word))
				{
					_confirmationWordsSourceList.Add(word);
				}
			}
		}
	}
}