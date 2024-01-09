using ReactiveUI;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class PasswordFinderModel : ReactiveObject
{
	private readonly Subject<(int Percentage, TimeSpan RemainingTime)> _progress;
	private readonly Wallet _wallet;

	public PasswordFinderModel(IWalletModel walletModel, Wallet wallet, string likelyPassword)
	{
		_wallet = wallet;
		_progress = new Subject<(int, TimeSpan)>();
		Wallet = walletModel;
		LikelyPassword = likelyPassword;
	}

	public IWalletModel Wallet { get; }

	public Charset Charset { get; set; }

	public bool UseNumbers { get; set; }

	public bool UseSymbols { get; set; }

	public string LikelyPassword { get; }

	public IObservable<(int Percentage, TimeSpan RemainingTime)> Progress => _progress.ObserveOn(RxApp.MainThreadScheduler);

	public async Task<(bool, string?)> FindPasswordAsync(CancellationToken cancellationToken)
	{
		var options = new PasswordFinderOptions(_wallet, LikelyPassword)
		{
			Charset = Charset,
			UseNumbers = UseNumbers,
			UseSymbols = UseSymbols,
		};
		string? foundPassword = null;
		var result = await Task.Run(() => PasswordFinderHelper.TryFind(options, out foundPassword, SetStatus, cancellationToken));

		return (result, foundPassword);
	}

	private void SetStatus(int percentage, TimeSpan remainingTime)
	{
		_progress.OnNext((percentage, remainingTime));
	}
}
