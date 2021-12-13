using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.BranchNBound
{
	public class Selector
	{
		public Money CalcEffectiveValue(List<Money> list)
		{
			Money sum = Money.Satoshis(0);
			if (list is not { })
			{
				return sum;
			}

			foreach (var item in list)
			{
				sum += item.Satoshi;        // TODO: effectiveValue = utxo.value − feePerByte × bytesPerInput
			}

			return sum;
		}
	}
}
