using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public enum TestRoundStatusEnum
	{
		New,
		Started,
		Signing,
		Succeeded,
		Failed,
	}
}
