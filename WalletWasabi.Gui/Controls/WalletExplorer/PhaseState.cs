using WalletWasabi.CoinJoin.Common.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public struct PhaseState
	{
		public PhaseState(RoundPhase phase, bool error)
		{
			Phase = phase;
			Error = error;
		}

		public RoundPhase Phase { get; }

		public bool Error { get; }
	}
}
