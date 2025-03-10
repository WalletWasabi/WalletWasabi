namespace WalletWasabi.BitcoinRpc.Models;

public enum RpcPubkeyType
{
	Unknown,
	TxNonstandard,
	TxPubkey,
	TxPubkeyhash,
	TxScripthash,
	TxMultisig,
	TxNullData,
	TxWitnessV0Keyhash,
	TxWitnessV0Scripthash,
	TxWitnessV1Taproot,
	TxWitnessUnknown,
}
