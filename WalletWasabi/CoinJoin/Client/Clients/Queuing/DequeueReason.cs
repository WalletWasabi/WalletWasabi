using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.CoinJoin.Client.Clients.Queuing
{
	public enum DequeueReason
	{
		UserRequested, // Success, user requested the dequeue.
		TransactionBuilding, // Success, transaction is being build with the coins.
		Banned, // Success
		NotEnoughFundsEnqueued, // Success
		CoordinatorFeeChanged, // Success
		ApplicationExit, // Success, application is exiting.
		Spent, // Success, coin is spent.
		Mixing // Failure, the coin's mixing status is > registration.
	}
}
