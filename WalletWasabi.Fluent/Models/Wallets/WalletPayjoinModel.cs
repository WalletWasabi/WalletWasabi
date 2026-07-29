using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Payjoin;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

/// <summary>
/// UI-facing seam over <see cref="PayjoinManager"/> for one wallet, in the mold of
/// <see cref="WalletCoinjoinModel"/>.
/// </summary>
[AppLifetime]
public class WalletPayjoinModel
{
	private readonly Wallet _wallet;
	private readonly PayjoinManager _manager;

	public WalletPayjoinModel(Wallet wallet, PayjoinManager manager)
	{
		_wallet = wallet;
		_manager = manager;

		SessionStates = Observable
			.FromEventPattern<PayjoinSessionState>(h => _manager.SessionStatusChanged += h, h => _manager.SessionStatusChanged -= h)
			.Select(x => x.EventArgs);
	}

	/// <summary>Payjoin receiving needs hot programmatic signing, so hardware and watch-only wallets are out.</summary>
	public bool IsAvailable => !_wallet.KeyManager.IsHardwareWallet && !_wallet.KeyManager.IsWatchOnly;

	public IObservable<PayjoinSessionState> SessionStates { get; }

	public Task<PayjoinSessionState> StartReceiveSessionAsync(string address, CancellationToken cancellationToken) =>
		_manager.StartReceiverSessionAsync(_wallet, address, cancellationToken);
}
