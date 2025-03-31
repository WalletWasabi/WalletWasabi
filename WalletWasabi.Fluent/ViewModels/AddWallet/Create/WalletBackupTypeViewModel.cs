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

	private WalletBackupTypeViewModel(WalletCreationOptions options)
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

	private void OnNext(WalletCreationOptions options)
	{
		switch (_walletBackupType)
		{
			case WalletBackupType.RecoveryWords:
			{
				switch (options)
				{
					case WalletCreationOptions.AddNewWallet add:
					{
						var recoveryWordsBackup = add.WalletBackups?.FirstOrDefault(x => x is RecoveryWordsBackup);
						add = add with
						{
							SelectedWalletBackup = recoveryWordsBackup
						};

						Navigate().To().RecoveryWords(add);
						break;
					}
					case WalletCreationOptions.RecoverWallet rec:
					{
						Navigate().To().RecoverWallet(rec);
						break;
					}
					default:
					{
						throw new ArgumentOutOfRangeException();
					}
				}
				break;
			}
			case WalletBackupType.MultiShare:
			{
				switch (options)
				{
					case WalletCreationOptions.AddNewWallet add:
					{
						var multiShareBackup = add.WalletBackups?.FirstOrDefault(x => x is MultiShareBackup);
						add = add with
						{
							SelectedWalletBackup = multiShareBackup
						};

						Navigate().To().MultiShareOptions(add);
						break;
					}
					case WalletCreationOptions.RecoverWallet rec:
					{
						// TODO:
						break;
					}
					default:
					{
						throw new ArgumentOutOfRangeException();
					}
				}


				break;
			}
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
