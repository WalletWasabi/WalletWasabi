using System.Collections.Generic;

namespace WalletWasabi.Tor.NetworkChecker;

public interface IIssueListParser
{
	IEnumerable<Issue> Parse(string str);
}
