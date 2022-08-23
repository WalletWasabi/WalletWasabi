using WalletWasabi.WabiSabiClientLibrary.Models.GetAnonymityScores;

namespace WalletWasabi.WabiSabiClientLibrary.Models;

public record GetAnonymityScoresRequest(
	Tx[] Transactions
);
