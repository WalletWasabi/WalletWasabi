using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletAuthModel : ReactiveObject
{
	private readonly IServices _services;
	private readonly WalletId _walletId;
	[AutoNotify] private bool _isLoggedIn;

	public WalletAuthModel(IServices services, WalletId walletId)
	{
		_services = services;
		_walletId = walletId;
	}

	public bool HasPassword => _services.HasPassword(_walletId);

	public async Task LoginAsync(string password)
	{
		var isPasswordCorrect = await _services.TryLoginAsync(_walletId, password).ConfigureAwait(false);
		if (!isPasswordCorrect)
		{
			throw new InvalidOperationException("Incorrect passphrase.");
		}

		CompleteLogin();
	}

	public async Task<WalletLoginResult> TryLoginAsync(string password)
	{
		var (isPasswordCorrect, compatibilityPassword) = await _services.TryLoginWithCompatibilityAsync(_walletId, password).ConfigureAwait(false);
		var compatibilityPasswordUsed = compatibilityPassword is { };

		return new(isPasswordCorrect, compatibilityPasswordUsed);
	}

	public async Task<bool> TryPasswordAsync(string password)
	{
		return await _services.IsPasswordValidAsync(_walletId, password).ConfigureAwait(false);
	}

	public void CompleteLogin()
	{
		IsLoggedIn = true;
	}

	public void Logout()
	{
		_services.Logout(_walletId);
		IsLoggedIn = false;
	}

	public async Task<bool> VerifyRecoveryWordsAsync(Mnemonic mnemonic)
	{
		return await _services.VerifyRecoveryWordsAsync(_walletId, mnemonic).ConfigureAwait(false);
	}
}
