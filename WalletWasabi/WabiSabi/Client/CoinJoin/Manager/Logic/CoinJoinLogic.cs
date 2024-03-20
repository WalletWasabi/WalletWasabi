using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Manager.Logic;

public static class CoinJoinLogic
{
	public static SynchronizeResponse AssertStartCoinJoin(bool walletBlockedByUi, IWallet wallet, bool overridePlebStop, IWasabiBackendStatusProvider wasabiBackendStatusProvider)
	{
		if (walletBlockedByUi)
		{
			throw new CoinJoinClientException(CoinjoinError.UserInSendWorkflow);
		}

		if (!overridePlebStop && wallet.IsUnderPlebStop)
		{
			throw new CoinJoinClientException(CoinjoinError.NotEnoughUnprivateBalance);
		}

		if (wasabiBackendStatusProvider.LastResponse is not { } synchronizerResponse)
		{
			throw new CoinJoinClientException(CoinjoinError.BackendNotSynchronized);
		}

		return synchronizerResponse;
	}
}
