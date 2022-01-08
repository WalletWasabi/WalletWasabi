using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public record WalletStatusChangedEventArgs(Wallet Wallet, bool IsCoinJoining);
