using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Blockchain.BlockFilters.History
{
	public class ActionItem
	{
		public Operation Action { get; }
		public OutPoint OutPoint { get; }
		public Script Script { get; }

		public ActionItem(Operation action, OutPoint outPoint, Script script)
		{
			Action = action;
			OutPoint = outPoint;
			Script = script;
		}
	}
}
