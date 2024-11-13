using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Models;

public record TransactionBroadcastInfo(string TransactionId, int InputCount, int OutputCount, Amount? InputAmount, Amount? OutputAmount, Amount? NetworkFee);
