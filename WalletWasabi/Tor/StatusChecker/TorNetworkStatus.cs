using System.Collections.Generic;

namespace WalletWasabi.Tor.StatusChecker;
public class TorNetworkStatus
{
	public List<SystemItem> Systems { get; set; }
}

public class SystemItem
{
	public string Name { get; set; }
	public string Description { get; set; }
	public string Category { get; set; }
	public string Status { get; set; }
	public List<TorIssue> UnresolvedIssues { get; set; }
}

public class TorIssue
{
	public string Is { get; set; }
	public string Title { get; set; }
	public string CreatedAt { get; set; }
	public string LastMod { get; set; }
	public string Permalink { get; set; }
	public string Severity { get; set; }
	public bool Resolved { get; set; }
	public bool Informational { get; set; }
	public string ResolvedAt { get; set; }
	public List<string> Affected { get; set; }
	public string Filename { get; set; }
}
