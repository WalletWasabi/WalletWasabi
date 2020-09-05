using System.Collections.Generic;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public interface IStatementDescription
	{
		IEnumerable<GroupElement> PublicPoints { get; }
		IEnumerable<GroupElement> Generators { get; }
	}
}
