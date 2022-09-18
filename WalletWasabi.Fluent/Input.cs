using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

namespace WalletWasabi.Fluent;

internal record Input(Money Amount, string Address, bool IsSpent) : ITransferredAmount;
