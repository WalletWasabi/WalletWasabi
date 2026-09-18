namespace WalletWasabi.WebClients.PayJoin;

public interface IPayjoinClient
{
	Uri PaymentUrl { get; }

	Task<PSBT> RequestPayjoin(PSBT originalTx, IHDKey segwitAccountKey, RootedKeyPath segwitRootedKeyPath, IHDKey? taprootAccountKey, RootedKeyPath taprootRootedKeyPath, HdPubKey? changeHdPubKey, CancellationToken cancellationToken);
}
