using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IPasswordFinderModel
{
	IWalletModel Wallet { get; }
	Charset Charset { get; set; }
	string LikelyPassword { get; }
	bool UseNumbers { get; set; }
	bool UseSymbols { get; set; }

	Task<(bool, string?)> FindPasswordAsync(CancellationToken cancellationToken);

	IObservable<(int Percentage, TimeSpan RemainingTime)> Progress { get; }
}
