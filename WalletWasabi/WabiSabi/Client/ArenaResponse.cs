using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Client
{
	public class ArenaResponse
	{
		public ArenaResponse(IEnumerable<Credential> realAmountCredentials, IEnumerable<Credential> realVsizeCredentials)
		{
			RealAmountCredentials = realAmountCredentials.ToArray();
			RealVsizeCredentials = realVsizeCredentials.ToArray();
		}
		public Credential[] RealAmountCredentials { get; }
		public Credential[] RealVsizeCredentials { get; }
	}

	public class ArenaResponse<T> : ArenaResponse
	{
		public ArenaResponse(T value, IEnumerable<Credential> realAmountCredentials, IEnumerable<Credential> realVsizeCredentials)
			: base(realAmountCredentials, realVsizeCredentials)
		{
			Value = value;
		}

		public T Value { get; }
	}
}
