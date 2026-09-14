using System;
using NBitcoin;
using ReactiveUI;

namespace WalletWasabi.Fluent.Models.Wallets;

/// <summary>
/// A wallet-owned selection intent, independent of a desktop grid or mobile view.
/// All operations belong to the UI thread. Only the latest unconsumed request is retained.
/// </summary>
public sealed class TransactionSelection : ReactiveObject
{
	private TransactionSelectionRequest? _pending;

	public TransactionSelectionRequest? Pending
	{
		get => _pending;
		private set => this.RaiseAndSetIfChanged(ref _pending, value);
	}

	public TransactionSelectionRequest Request(uint256 transactionId)
	{
		ArgumentNullException.ThrowIfNull(transactionId);
		var request = new TransactionSelectionRequest(transactionId);
		Pending = request;
		return request;
	}

	public bool TryConsume(TransactionSelectionRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (!ReferenceEquals(Pending, request)) return false;
		Pending = null;
		return true;
	}
}

/// <summary>Reference identity distinguishes repeated requests for the same transaction.</summary>
public sealed class TransactionSelectionRequest
{
	internal TransactionSelectionRequest(uint256 transactionId) => TransactionId = transactionId;
	public uint256 TransactionId { get; }
}
