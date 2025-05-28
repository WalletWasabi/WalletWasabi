using System.Collections.Generic;

namespace WalletWasabi.Tor.StatusChecker;
public record TorNetworkStatus(List<SystemItem> Systems);
public record SystemItem(string Name, string Status, List<TorIssue> UnresolvedIssues);
public record TorIssue(string Title, bool Resolved, List<string> Affected);
