using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletAuthModel
{
	bool IsLoggedIn { get; }

	bool IsLegalRequired { get; }

	Task<WalletLoginResult> TryLoginAsync(string password);

	Task LoginAsync(string password);

	Task<bool> TryPasswordAsync(string password);

	Task AcceptTermsAndConditions();

	void CompleteLogin();

	void Logout();

	IPasswordFinderModel GetPasswordFinder(string likelyPassword);
}
