using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client;

public record SmartCoinAndSecret(SmartCoin Coin, BitcoinSecret Secret);
