using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

namespace WalletWasabi.Fluent;

public record Input(Money Amount, string Address, bool IsSpent) : ITransferredAmount;
