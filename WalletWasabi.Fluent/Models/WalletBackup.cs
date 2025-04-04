using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets.Slip39;

namespace WalletWasabi.Fluent.Models;

public abstract record WalletBackup(string Password);

public record RecoveryWordsBackup(
	string Password,
	Mnemonic Mnemonic) : WalletBackup(Password);

public record MultiShareBackupSettings(
	byte Threshold = WalletGenerator.DefaultShamirThreshold,
	byte Shares = WalletGenerator.DefaultShamirShares);

public record MultiShareBackup(
	MultiShareBackupSettings Settings,
	string Password,
	Share[] Shares,
	byte CurrentShare = 0,
	byte CurrentSharePage = 0) : WalletBackup(Password);
