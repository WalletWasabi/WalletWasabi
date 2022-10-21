using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.Affiliation.Models.PaymentData;
using WalletWasabi.Affiliation.Extensions;
using System.Collections.Concurrent;

namespace WalletWasabi.Affiliation;

public class RoundData
{
	public RoundData()
	{
		Inputs = new();
		RoundParameters = null;
		Transaction = null;
	}

	private RoundParameters? RoundParameters { get; set; }
	private NBitcoin.Transaction? Transaction { get; set; }
	private bool IsLocked { get; set; } = false;

	private ConcurrentBag<AffiliateCoin> Inputs { get; }

	public void Lock()
	{
		if (!IsReady())
		{
			throw new InvalidOperationException("Round data are not ready.");
		}

		IsLocked = true;
	}

	public bool IsReady()
	{
		return (Transaction is not null) && (RoundParameters is not null);
	}

	public void AddTransaction(NBitcoin.Transaction transaction)
	{
		if (Transaction is not null)
		{
			throw new InvalidOperationException("Transaction was already set.");
		}
		Transaction = transaction;
	}

	public void AddRoundParameters(RoundParameters roundParameters)
	{
		if (RoundParameters is not null)
		{
			throw new InvalidOperationException("Round parameters were already set.");
		}
		RoundParameters = roundParameters;
	}

	public void AddInput(Coin coin, AffiliationFlag affiliationFlag, bool zeroCoordinationFee)
	{
		if (IsLocked)
		{
			throw new InvalidOperationException("Inputs cannot be added no more.");
		}

		Inputs.Add(new AffiliateCoin(coin, affiliationFlag, zeroCoordinationFee));
	}

	public Body GetAffiliationData(AffiliationFlag affiliationFlag)
	{
		if (!IsLocked)
		{
			throw new InvalidOperationException("Round data is not locked.");
		}

		if (!IsReady())
		{
			throw new InvalidOperationException("Round data is not ready.");
		}

		return GetAffiliationData(RoundParameters, Inputs, Transaction, affiliationFlag);
	}

	private static Body GetAffiliationData(RoundParameters roundParameters, IEnumerable<AffiliateCoin> Inputs, NBitcoin.Transaction transaction, AffiliationFlag affiliationFlag)
	{
		if (!transaction.Inputs.Select(x => x.PrevOut).ToHashSet().SetEquals(Inputs.Select(x => x.Outpoint).ToHashSet()))
		{
			throw new Exception("Inconsistent data.");
		}

		IEnumerable<Input> inputs = Inputs.Select(x => Input.FromCoin(x, x.ZeroCoordinationFee, x.AffiliationFlag == affiliationFlag));
		IEnumerable<Output> outputs = transaction.Outputs.Select<TxOut, Output>(x => Output.FromTxOut(x));

		return new Body(inputs, outputs, roundParameters.Network.ToSlip44CoinType(), roundParameters.CoordinationFeeRate.Rate, roundParameters.CoordinationFeeRate.PlebsDontPayThreshold.Satoshi, roundParameters.AllowedInputAmounts.Min.Satoshi, GetUnixTimestamp());
	}

	private static long GetUnixTimestamp()
	{
		return ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
	}

	private class AffiliateCoin : Coin
	{
		public AffiliateCoin(Coin coin, AffiliationFlag affiliationFlag, bool zeroCoordinationFee) : base(coin.Outpoint, coin.TxOut)
		{
			AffiliationFlag = affiliationFlag;
			ZeroCoordinationFee = zeroCoordinationFee;
		}

		public AffiliationFlag AffiliationFlag { get; }
		public bool ZeroCoordinationFee { get; }
	}
}
