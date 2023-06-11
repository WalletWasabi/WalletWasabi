using NBitcoin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.TransactionProcessing;

public class SafetyCoinjoins
{
	// Temporarily disabling CS8618 warning for non-nullable properties that are not initialized in the constructor.
	// This is because these properties ('RecentTransactions', 'PrevBalance', 'TrailingCoinjoinCountRequirement') are
	// guaranteed to be non-null as they are always initialized in the 'SetStartingConditions' method which is called from
	// the constructor. However, the compiler does not take into account method calls within the constructor when issuing
	// CS8618 warnings.
	//
	// We've considered several alternative approaches to resolve this warning:
	// 1. Making the properties nullable: This was dismissed as it would defeat the purpose of having non-nullable properties
	//    in the first place. We want to ensure that these properties always have a value.
	// 2. Initializing the properties directly in their declaration or constructor: This was dismissed to avoid duplicating
	//    code, as these properties are already initialized in the 'SetStartingConditions' method.
	// 3. Ignoring the warning: This was dismissed as it's generally not a good practice. Warnings often indicate potential
	//    issues in the code and shouldn't be ignored without a valid reason.
	//
	// As of now, we have ensured there's no code path that leaves these properties null. Therefore, it's safe to suppress
	// the CS8618 warning. We will keep an eye on updates to the .NET compiler that may enhance its ability to detect such
	// non-null guarantees.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	public SafetyCoinjoins()
	{
		SetStartingConditions();
	}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	private List<SmartTransaction> RecentTransactions { get; set; }
	private Money PrevBalance { get; set; }
	private int TrailingCoinjoinCountRequirement { get; set; }

	/// <summary>
	/// It should not matter that we fully mixed our wallet until the safety coinjoin mechanism isn't satisfied.
	/// </summary>
	public bool DoSafetyCoinjoin { get; private set; }

	/// <param name="prevBalance">Wallet balance before the transaction was added.</param>
	public void Process(SmartTransaction tx, Money prevBalance)
	{
		// If receive or cj, add to RecentTransactions, but alway start with receive.
		// This will build a list that starts with receive and receive or coinjoins follow.
		var isRecentTransactionsEmpty = !RecentTransactions.Any();
		if (tx.IsReceiveTransaction())
		{
			// If this is the first transaction after safety coinjoin conditions were satisfied, then remember prevBalance.
			if (isRecentTransactionsEmpty)
			{
				PrevBalance = prevBalance;
			}

			RecentTransactions.Add(tx);
		}
		else if (tx.IsOwnCoinjoin() && !isRecentTransactionsEmpty)
		{
			RecentTransactions.Add(tx);
		}

		var receiveSum = RecentTransactions.Sum(x => x.WalletOutputs.Where(x => !x.HdPubKey.IsInternal).Sum(x => x.Amount));
		var trailingCoinjoinCount = 0;
		for (int i = RecentTransactions.Count - 1; i >= 0; i--)
		{
			if (RecentTransactions[i].IsOwnCoinjoin())
			{
				trailingCoinjoinCount++;
			}
			else
			{
				break;
			}
		}

		// If the newly received coin is too big and trailing coinjoin requirement is not fullfilled,
		// then we signal the need for safety coinjoins.
		// Otherwise we clear everything restart everything.
		if (receiveSum > PrevBalance / 2 && trailingCoinjoinCount < TrailingCoinjoinCountRequirement)
		{
			DoSafetyCoinjoin = true;
		}
		else
		{
			SetStartingConditions();
		}
	}

	private void SetStartingConditions()
	{
		DoSafetyCoinjoin = false;
		RecentTransactions = new();
		PrevBalance = Money.Zero;
		TrailingCoinjoinCountRequirement = Random.Shared.Next(2, 3);
	}
}
