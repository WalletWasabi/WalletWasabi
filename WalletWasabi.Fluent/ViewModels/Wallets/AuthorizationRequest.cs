using WalletWasabi.Fluent.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class AuthorizationRequest
{
	public Wallet Wallet { get; }
	public TransactionAuthorizationInfo TransactionAuthorizationInfo { get; }

	public AuthorizationRequest(Wallet wallet, TransactionAuthorizationInfo transactionAuthorizationInfo)
	{
		Wallet = wallet;
		TransactionAuthorizationInfo = transactionAuthorizationInfo;
	}

	public bool IsHardwareWallet => Wallet.KeyManager.IsHardwareWallet;
}