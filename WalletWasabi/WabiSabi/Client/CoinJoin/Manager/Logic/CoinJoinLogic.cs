using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Manager.Logic;

public static class CoinJoinLogic
{
	public static async Task<CoinjoinError> CheckWalletStartCoinJoinAsync(IWallet wallet, bool walletBlockedByUi, bool overridePlebStop)
	{
		// CoinJoin blocked by the UI. User is in action.
		if (walletBlockedByUi)
		{
			return CoinjoinError.UserInSendWorkflow;
		}

		// If payments are batched we always mix.
		if (wallet.BatchedPayments.AreTherePendingPayments)
		{
			return CoinjoinError.None;
		}

		// The wallet is already private.
		if (await wallet.IsWalletPrivateAsync().ConfigureAwait(false))
		{
			return CoinjoinError.AllCoinsPrivate;
		}

		// Wallet total balance is lower then the PlebStop threshold. If the user not overrides that, we won't mix.
		if (!overridePlebStop && wallet.IsUnderPlebStop)
		{
			return CoinjoinError.NotEnoughUnprivateBalance;
		}

		return CoinjoinError.None;
	}
}
