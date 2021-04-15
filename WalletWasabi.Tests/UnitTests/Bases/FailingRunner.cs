using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;

namespace WalletWasabi.Tests.UnitTests.Bases
{
	public class FailingRunner : PeriodicRunner
	{
		public FailingRunner() : base(TimeSpan.FromSeconds(1))
		{
		}

		protected override Task ActionAsync(CancellationToken cancel)
		{
			throw new NotImplementedException();
		}
	}
}
