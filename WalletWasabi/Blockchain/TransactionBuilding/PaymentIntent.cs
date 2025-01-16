using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets.SilentPayment;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public class PaymentIntent
{
	public PaymentIntent(Script scriptPubKey, Money amount, bool subtractFee = false, LabelsArray? label = null)
		: this(new DestinationRequest(scriptPubKey, MoneyRequest.Create(amount, subtractFee), label))
	{
	}

	public PaymentIntent(Script scriptPubKey, MoneyRequest amount, LabelsArray? label = null)
		: this(new DestinationRequest(scriptPubKey, amount, label))
	{
	}

	public PaymentIntent(Destination destination, Money amount, bool subtractFee = false, LabelsArray? label = null)
		: this(new DestinationRequest(destination, MoneyRequest.Create(amount, subtractFee), label))
	{
	}

	public PaymentIntent(Key destination, Money amount, bool subtractFee = false, LabelsArray? label = null)
		: this(new DestinationRequest(destination, MoneyRequest.Create(amount, subtractFee), label))
	{
	}

	public PaymentIntent(IDestination destination, MoneyRequest amount, LabelsArray? label = null)
		: this(new DestinationRequest(destination.ScriptPubKey, amount, label))
	{
	}

	public PaymentIntent(SilentPaymentAddress address, Money amount, bool subtractFee = false, LabelsArray? label = null)
		: this(new DestinationRequest(address, MoneyRequest.Create(amount, subtractFee), label))
	{
	}

	public PaymentIntent(SilentPaymentAddress address, MoneyRequest amount, LabelsArray? label = null)
		: this(new DestinationRequest(address, amount, label))
	{
	}

	public PaymentIntent(params DestinationRequest[] requests)
		: this(requests as IEnumerable<DestinationRequest>)
	{
	}

	public PaymentIntent(IEnumerable<DestinationRequest> requests)
	{
		Guard.NotNullOrEmpty(nameof(requests), requests);
		foreach (var request in requests)
		{
			Guard.NotNull(nameof(request), request);
		}

		var subtractFeeCount = requests.Count(x => x.Amount.SubtractFee);
		if (subtractFeeCount > 1)
		{
			// Note: It'd be possible to implement that the fees to be subtracted equally from more outputs, but I guess nobody would use it.
			throw new ArgumentException($"Only one request can specify fee subtraction.");
		}

		var allRemainingCount = requests.Count(x => x.Amount is MoneyRequest.AllRemaining);
		var changeCount = requests.Count(x => x.Amount is MoneyRequest.Change);
		int specialCount = allRemainingCount + changeCount;
		if (specialCount == 0)
		{
			ChangeStrategy = ChangeStrategy.Auto;
		}
		else if (specialCount == 1)
		{
			if (subtractFeeCount != 1)
			{
				throw new ArgumentException("You must specify fee subtraction strategy if custom change is specified.");
			}

			if (allRemainingCount == 1)
			{
				ChangeStrategy = ChangeStrategy.AllRemainingCustom;
			}
			else if (changeCount == 1)
			{
				ChangeStrategy = ChangeStrategy.Custom;
			}
			else
			{
				throw new NotSupportedException("This should never happen.");
			}
		}
		else
		{
			throw new ArgumentException("Only one request can contain an all remaining money or change request.");
		}

		Requests = requests;

		TotalAmount = requests.Select(x => x.Amount).OfType<MoneyRequest.Value>().Sum(x => x.Amount);
	}

	public IEnumerable<DestinationRequest> Requests { get; }
	public ChangeStrategy ChangeStrategy { get; }
	public Money TotalAmount { get; }

	public bool TryGetCustomRequest([NotNullWhen(true)] out DestinationRequest? request)
	{
		request = Requests.SingleOrDefault(x => x.Amount is MoneyRequest.Change or MoneyRequest.AllRemaining);

		return request is not null;
	}
}
