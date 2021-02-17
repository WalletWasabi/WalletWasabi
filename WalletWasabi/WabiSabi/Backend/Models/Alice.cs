using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Models
{
	public class Alice
	{
		public Alice(OutPoint outPoint)
		{
			OutPoint = outPoint;
		}

		public Guid Id { get; } = Guid.NewGuid();
		public OutPoint OutPoint { get; }
	}
}
