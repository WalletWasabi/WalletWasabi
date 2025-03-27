using System.Collections.Generic;
using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets.Slip39;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Multi-share")]
public partial class MultiShareViewModel : RoutableViewModel
{
	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte _currentShare;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte _totalShares;

	private MultiShareViewModel(WalletCreationOptions.AddNewWallet options)
	{
		var multiShareBackup = options.SelectedWalletBackup as MultiShareBackup;

		ArgumentNullException.ThrowIfNull(multiShareBackup);
		ArgumentNullException.ThrowIfNull(multiShareBackup.Shares);

		_currentShare = multiShareBackup.CurrentShare;
		_totalShares = multiShareBackup.Settings.Shares;

		MnemonicWords = CreateList(multiShareBackup.Shares[_currentShare - 1]);

		EnableBack = true;

		NextCommand = ReactiveCommand.Create(() => OnNext(options));

		CancelCommand = ReactiveCommand.Create(OnCancel);
	}

	public List<RecoveryWordViewModel> MnemonicWords { get; }

	private void OnNext(WalletCreationOptions.AddNewWallet options)
	{
		if (options.SelectedWalletBackup is not MultiShareBackup multiShareBackup)
		{
			throw new ArgumentOutOfRangeException(nameof(options));
		}

		if (multiShareBackup.Shares is null)
		{
			throw new ArgumentNullException(nameof(options));
		}

		if (_currentShare < multiShareBackup.Settings.Shares)
		{
			options = options with
			{
				SelectedWalletBackup = multiShareBackup with
				{
					CurrentShare = ++_currentShare
				}
			};

			Navigate().To().MultiShare(options);
		}
		else
		{
			options = options with
			{
				SelectedWalletBackup = multiShareBackup with
				{
					CurrentShare = 1
				}
			};

			var wordsDictionary = new Dictionary<int, List<RecoveryWordViewModel>>();

			for (var i = 0; i < _totalShares; i++)
			{
				var words = CreateList(multiShareBackup.Shares[i]);
				wordsDictionary[i] = words;
			}

			Navigate().To().ConfirmMultiShare(options, wordsDictionary);
		}
	}

	private void OnCancel()
	{
		Navigate().Clear();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private List<RecoveryWordViewModel> CreateList(Share share)
	{
		var result = new List<RecoveryWordViewModel>();
		var words = share.ToMnemonic(WordList.Wordlist).Split(' ', StringSplitOptions.RemoveEmptyEntries);

		for (var i = 0; i < words.Length; i++)
		{
			result.Add(new RecoveryWordViewModel(i + 1, words[i]));
		}

		return result;
	}
}
