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
		switch (options)
		{
			case WalletCreationOptions.AddNewWallet:
			{
				_walletBackupTypes =
				[
					new WalletBackupType.MultiShare(
						new WalletBackupTypeOptions(
							Description: "Multi-share Backup",
							HelpText: "Split your wallet backup into multiple parts. You’ll need some of them to restore your wallet, adding an extra layer of protection.",
							ToolTipText: "Uses Shamir's Secret Sharing (SLIP-0039) to divide the wallet secret into multiple shares. A defined number of these shares are required to reconstruct the wallet’s master secret.")),
					new WalletBackupType.RecoveryWords(
						new WalletBackupTypeOptions(
							Description: "Recovery words Backup",
							HelpText: "Back up your wallet using a set of secret words. Write them down and store them safely — you’ll need them to recover your wallet.",
							ToolTipText: "Creates a BIP39 mnemonic phrase (12 words) that encodes the wallet's seed. This phrase can regenerate your private keys and restore access to your funds on compatible wallets.")),
				];

				break;
			}
			case WalletCreationOptions.RecoverWallet:
			{
				_walletBackupTypes =
				[
					new WalletBackupType.RecoveryWords(
						new WalletBackupTypeOptions(
							Description: "Single mnemonic phrase (BIP39)")),
					new WalletBackupType.MultiShare(
						new WalletBackupTypeOptions(
							Description: "Parts of Shamir's Secret Sharing (SLIP-0039)"))
				];

				break;
			}
			default:
			{
				throw new ArgumentOutOfRangeException();
			}
		}

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
						Navigate().To().RecoverMultiShareWallet(rec);
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
