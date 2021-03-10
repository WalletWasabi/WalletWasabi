using WalletWasabi.Models;

namespace WalletWasabi.CoinJoin.Client.Clients.Queuing
{
	public enum DequeueReason
	{
		[FriendlyName("User requested.")]
		UserRequested, // Success, user requested the dequeue.

		[FriendlyName("Using coin for transaction building.")]
		TransactionBuilding, // Success, transaction is being built with the coins.

		[FriendlyName("Coin is banned.")]
		Banned, // Success

		[FriendlyName("Not enough funds enqueued.")]
		NotEnoughFundsEnqueued, // Success

		[FriendlyName("Coordinator fee changed.")]
		CoordinatorFeeChanged, // Success

		[FriendlyName("Application is exiting.")]
		ApplicationExit, // Success, application is exiting.

		[FriendlyName("Coin is spent.")]
		Spent, // Success, coin is spent.

		[FriendlyName("Coin is being mixed.")]
		Mixing // Failure, the coin's mixing status is > registration.
	}
}
