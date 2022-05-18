namespace WalletWasabi.WabiSabiClientLibrary.Models;

/// <summary>
/// Response object for <see cref="SelectInputsForRoundRequest"/>.
/// </summary>
/// <param name="Indices">Indices of the selected UTXOs from the corresponding <see cref="SelectInputsForRoundRequest.Utxos"/> array.</param>
public record SelectInputsForRoundResponse(
	int[] Indices
);
