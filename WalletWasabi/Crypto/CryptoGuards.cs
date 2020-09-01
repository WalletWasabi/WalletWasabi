using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto
{
	public static class CryptoGuard
	{
		[DebuggerStepThrough]
		public static GroupElement NotInfinity(string parameterName, GroupElement groupElement)
			=> groupElement.IsInfinity switch
			{
				true => throw new ArgumentException("Point at infinity is not a valid value.", parameterName),
				false => groupElement
			};

		[DebuggerStepThrough]
		public static IEnumerable<GroupElement> NotInfinity(string parameterName, IEnumerable<GroupElement> groupElements)
			=> groupElements.Select((ge, i) => NotInfinity($"{parameterName}[{i}]", ge));
	}
}
