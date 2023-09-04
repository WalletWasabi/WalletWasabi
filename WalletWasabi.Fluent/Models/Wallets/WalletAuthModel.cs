using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletAuthModel : ReactiveObject
{
	private readonly IWalletModel _walletModel;
	private readonly Wallet _wallet;
	[AutoNotify] private bool _isLoggedIn;

	public WalletAuthModel(IWalletModel walletModel, Wallet wallet)
	{
		_walletModel = walletModel;
		_wallet = wallet;
	}

	public bool IsLegalRequired => Services.LegalChecker.TryGetNewLegalDocs(out _);

	public async Task<WalletLoginResult> TryLoginAsync(string password)
	{
		string? compatibilityPassword = null;
		var isPasswordCorrect = await Task.Run(() => _wallet.TryLogin(password, out compatibilityPassword));

		var compatibilityPasswordUsed = compatibilityPassword is { };

		return new(isPasswordCorrect, compatibilityPasswordUsed);
	}

	public async Task<bool> TryPasswordAsync(string password)
	{
		return await Task.Run(() => PasswordHelper.TryPassword(_wallet.KeyManager, password, out _));
	}

	public async Task AcceptTermsAndConditions()
	{
		await Services.LegalChecker.AgreeAsync();
	}

	public void CompleteLogin()
	{
		IsLoggedIn = true;
	}

	public void Logout()
	{
		_wallet.Logout();
		IsLoggedIn = false;
	}

	public IPasswordFinderModel GetPasswordFinder(string password)
	{
		return new PasswordFinderModel(_walletModel, _wallet, password);
	}
}
