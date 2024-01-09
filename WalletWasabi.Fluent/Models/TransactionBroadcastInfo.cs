namespace WalletWasabi.Fluent.Models;

public record TransactionBroadcastInfo(string TransactionId, int InputCount, int OutputCount, string InputAmoutString, string OutputAmountString, string FeeString);
