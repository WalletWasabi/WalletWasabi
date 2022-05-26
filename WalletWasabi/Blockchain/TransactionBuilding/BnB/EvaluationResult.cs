namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

public enum EvaluationResult
{
	/// <summary>Select a value to the selection and continue on.</summary>
	Continue,

	/// <summary>Omit a value from the selection.</summary>
	SkipBranch,

	/// <summary>Match was found!</summary>
	Match
}
