using HiddenWallet.ChaumianCoinJoin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
    public class TumblerStateMachine
    {
		public string Phase { get; private set; } = TumblerPhase.InputRegistration;
		public async Task StartAsync(CancellationToken cancel)
		{
			while (true)
			{
				try
				{
					if (cancel.IsCancellationRequested) return;

					switch(Phase)
					{
						case TumblerPhase.InputRegistration:
							{
								break;
							}
						case TumblerPhase.InputConfirmation:
							{
								break;
							}
						case TumblerPhase.OutputRegistration:
							{
								break;
							}
						case TumblerPhase.Signing:
							{
								break;
							}
						default:
							{
								throw new NotSupportedException("This should never happen");
							}
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Ignoring {nameof(TumblerStateMachine)} exception: {ex}");
				}
			}
		}
	}
}
