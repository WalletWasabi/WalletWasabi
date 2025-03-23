using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Wallet Backup Type")]
public partial class WalletBackupTypeViewModel : RoutableViewModel
{
	[AutoNotify] private WalletBackupType _walletBackupType;
	[AutoNotify] private List<WalletBackupType> _walletBackupTypes;

	private WalletBackupTypeViewModel(WalletCreationOptions.AddNewWallet options)
	{
		_walletBackupTypes = [
			new WalletBackupType.RecoveryWords(),
			new WalletBackupType.MultiShare()
		];

		_walletBackupType = _walletBackupTypes.First();

		EnableBack = true;

		NextCommand = ReactiveCommand.Create(() => OnNext(options));

		CancelCommand = ReactiveCommand.Create(OnCancel);
	}

	private void OnNext(WalletCreationOptions.AddNewWallet options)
	{
		switch (_walletBackupType)
		{
			case WalletBackupType.RecoveryWords:
				// TODO: Add wallet backup type to options
				// TODO: Maybe call AddNewWallet.WithNewMnemonic() here instead in AddWalletPageViewModel.OnCreateWallet()
				//       and initialize WalletBackup = new RecoveryWordsBackup(...) here.
				Navigate().To().RecoveryWords(options);
				break;
			case WalletBackupType.MultiShare:
				// TODO: Add wallet backup type to options
				// TODO: Navigate to Multi-share Backup
				break;
			default:
				throw new ArgumentOutOfRangeException();
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
}
