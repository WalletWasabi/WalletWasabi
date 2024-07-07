using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.Models;

public record AncestorCpfpInfo(uint256 TxId, long Fee, long Weight);
public record CpfpInfo(List<AncestorCpfpInfo> Ancestors, decimal EffectiveFeePerVSize, decimal AdjustedVSize);
