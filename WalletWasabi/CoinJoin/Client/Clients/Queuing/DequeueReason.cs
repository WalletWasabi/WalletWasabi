using System.ComponentModel;

namespace WalletWasabi.CoinJoin.Client.Clients.Queuing
{
	public enum DequeueReason
	{
		[Description("User requested.")]
		UserRequested, // Success, user requested the dequeue.

		[Description("Using coin for transaction building.")]
		TransactionBuilding, // Success, transaction is being built with the coins.

		[Description("Coin is banned.")]
		Banned, // Success

		[Description("Not enough funds enqueued.")]
		NotEnoughFundsEnqueued, // Success

		[Description("Coordinator fee changed.")]
		CoordinatorFeeChanged, // Success

		[Description("Application is exiting.")]
		ApplicationExit, // Success, application is exiting.

		[Description("Coin is spent.")]
		Spent, // Success, coin is spent.

		[Description("Coin is being mixed.")]
		Mixing // Failure, the coin's mixing status is > registration.
	}
}
