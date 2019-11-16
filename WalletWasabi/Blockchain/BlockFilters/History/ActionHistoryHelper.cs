using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Blockchain.BlockFilters.History
{
	public class ActionHistoryHelper
	{
		private List<ActionItem> ActionHistory { get; }

		public ActionHistoryHelper()
		{
			ActionHistory = new List<ActionItem>();
		}

		public void StoreAction(ActionItem actionItem)
		{
			ActionHistory.Add(actionItem);
		}

		public void StoreAction(Operation action, OutPoint outpoint, Script script)
		{
			StoreAction(new ActionItem(action, outpoint, script));
		}

		public void Rollback(Dictionary<OutPoint, Script> toRollBack)
		{
			for (var i = ActionHistory.Count - 1; i >= 0; i--)
			{
				ActionItem act = ActionHistory[i];
				switch (act.Action)
				{
					case Operation.Add:
						toRollBack.Remove(act.OutPoint);
						break;

					case Operation.Remove:
						toRollBack.Add(act.OutPoint, act.Script);
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}
			}
			ActionHistory.Clear();
		}
	}
}
