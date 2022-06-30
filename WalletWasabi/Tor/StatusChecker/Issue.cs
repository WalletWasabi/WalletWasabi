namespace WalletWasabi.Tor.StatusChecker;

public class Issue
{
	public Issue(string title, bool resolved)
	{
		Title = title;
		Resolved = resolved;
	}

	public string Title { get; }
	public bool Resolved { get; }

	public override string ToString()
	{
		return $"{nameof(Title)}: {Title}, {nameof(Resolved)}: {Resolved}";
	}
}
