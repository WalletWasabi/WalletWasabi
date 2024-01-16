using NBitcoin;

namespace WalletWasabi.Wallets.BlockProvider;

public record BlockWithSourceData(Block Block, ISourceData SourceData);
