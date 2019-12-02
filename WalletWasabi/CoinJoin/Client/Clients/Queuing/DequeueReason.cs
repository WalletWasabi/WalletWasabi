using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.CoinJoin.Client.Clients.Queuing
{
	public enum DequeueReason
	{
		UserRequested, // Success, user requested the dequeue.
		TransactionBuilding, // Success, transaction is being built with the coins.
		Banned, // Success
		NotEnoughFundsEnqueued, // Success
		CoordinatorFeeChanged, // Success
		ApplicationExit, // Success, application is exiting.
		Spent, // Success, coin is spent.
		Mixing // Failure, the coin's mixing status is > registration.
	}

	public static class DequeueReasonExtensions
	{
		public static string ToFriendlyString(this DequeueReason me)
		{
			return me switch
			{
				DequeueReason.UserRequested => "User requested.",
				DequeueReason.TransactionBuilding => "Using coin for transaction building.",
				DequeueReason.Banned => "Coin is banned.",
				DequeueReason.NotEnoughFundsEnqueued => "Not enough funds enqueued.",
				DequeueReason.CoordinatorFeeChanged => "Coordinator fee changed.",
				DequeueReason.ApplicationExit => "Application is exiting.",
				DequeueReason.Spent => "Coin is spent.",
				DequeueReason.Mixing => "Coin is being mixed.",
				_ => me.ToString()
			};
		}
	}
}
