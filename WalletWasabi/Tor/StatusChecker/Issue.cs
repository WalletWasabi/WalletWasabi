namespace WalletWasabi.Tor.StatusChecker;

public class Issue
{
	public Issue(string systemName, string issueTitle,bool resolved)
	{
		SystemName = systemName;
		IssueTitle = issueTitle;
		Resolved = resolved;
	}

	public string SystemName { get; }
	public string IssueTitle { get; }
	public bool Resolved { get; }

	public override string ToString()
	{
		return $"{nameof(SystemName)}: {SystemName}, {nameof(IssueTitle)}: {IssueTitle} ,{nameof(Resolved)}: {Resolved}";
	}
}
