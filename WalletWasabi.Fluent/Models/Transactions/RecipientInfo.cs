using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;

namespace WalletWasabi.Fluent.Models.Transactions;

public record RecipientInfo(
	Destination Destination,
	Money Amount,
	LabelsArray Label,
	bool IsSubtractFee = false);
