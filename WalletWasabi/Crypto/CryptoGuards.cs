using NBitcoin.Secp256k1;
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
		public static GroupElement NotNullOrInfinity(string parameterName, GroupElement groupElement)
			=> groupElement?.IsInfinity switch
			{
				null => throw new ArgumentNullException(parameterName),
				true => throw new ArgumentException("Point at infinity is not a valid value.", parameterName),
				false => groupElement
			};

		[DebuggerStepThrough]
		public static IEnumerable<GroupElement> NotNullOrInfinity(string parameterName, IEnumerable<GroupElement> groupElements)
			=> groupElements switch
			{
				null => throw new ArgumentNullException(parameterName),
				_ when !groupElements.Any() => throw new ArgumentException(parameterName),
				_ => groupElements.Select((ge, i) => NotNullOrInfinity($"{parameterName}[{i}]", ge)).ToList()
			};

		[DebuggerStepThrough]
		public static Scalar NotZero(string parameterName, Scalar scalar)
			=> scalar switch
			{
				_ when scalar.IsZero => throw new ArgumentException("Value cannot be zero.", parameterName),
				_ => scalar
			};
	}
}
