namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

public enum NextAction
{
	/// <summary>First try to include a value and then try not to include the value in the selection.</summary>
	IncludeFirstThenOmit,

	/// <summary>First try NOT to include a value and then try to include the value in the selection.</summary>
	OmitFirstThenInclude,

	/// <summary>Include value.</summary>
	Include,

	/// <summary>Omit value.</summary>
	Omit,

	/// <summary>Current selection is wrong, rolling back and trying different combination.</summary>
	Backtrack
}
