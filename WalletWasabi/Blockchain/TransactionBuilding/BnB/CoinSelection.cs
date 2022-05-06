using System.Linq;

namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Models a coin selection with <see cref="PaymentAmount">sum of coin effective values</see> and
/// <see cref="TotalCosts">total spending sum for the payer</see>.
/// </summary>
public class CoinSelection
{
	public CoinSelection(long paymentAmount, long sumWithCosts)
	{
		PaymentAmount = paymentAmount;
		TotalCosts = sumWithCosts;
		IncludedCoinsCount = 0;
	}

	/// <summary>Value in satoshi that the receiver will receive.</summary>
	public long PaymentAmount { get; private set; }

	/// <summary>Value in satoshi that the payer has to pay in total.</summary>
	/// <remarks><see cref="TotalCosts"/> is always greater than <see cref="PaymentAmount"/>.</remarks>
	public long TotalCosts { get; private set; }

	/// <summary>Number of non-zero values in <see cref="Selection"/>.</summary>
	/// <remarks>This is an optimization to avoid computing this value again and again.</remarks>
	public int IncludedCoinsCount { get; private set; }

	/// <remarks>Array items containing zeros mean that the coin is not part of the coin selection.</remarks>
	public long[]? Selection { get; private set; }

	/// <summary>Sets a new coin selection with its properties.</summary>
	/// <remarks>Old selection is forgotten.</remarks>
	public void Update(long paymentAmount, long totalCosts, int includedCoinsCount, long[] selection)
	{
		PaymentAmount = paymentAmount;
		TotalCosts = totalCosts;
		IncludedCoinsCount = includedCoinsCount;
		Selection = selection;
	}

	/// <summary>Gets an array with non-zero values in satoshis.</summary>
	public long[]? GetSolutionArray()
	{
		return Selection?.Where(x => x > 0).ToArray();
	}
}
