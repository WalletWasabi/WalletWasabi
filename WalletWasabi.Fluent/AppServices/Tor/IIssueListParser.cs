using System.Collections.Generic;

namespace WalletWasabi.Fluent.AppServices.Tor;

public interface IIssueListParser
{
	IEnumerable<Issue> Parse(string str);
}
