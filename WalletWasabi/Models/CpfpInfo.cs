using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.Models;

public record RelativeCpfpInfo(uint256 TxId, long Fee, long Weight);
public record CpfpInfo(List<RelativeCpfpInfo> Ancestors, List<RelativeCpfpInfo> Descendants, decimal EffectiveFeePerVSize, decimal AdjustedVSize);
