using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace WalletWasabi.Tor.StatusChecker;

public class XmlIssueListParser
{
	private const string ResolvedToken = "[resolved]";

	public IEnumerable<Issue> Parse(string xml)
	{
		var xmlDocument = new XmlDocument();
		xmlDocument.LoadXml(xml);
		var items = xmlDocument.SelectNodes("//rss/channel/item");
		return items!
			.Cast<XmlElement>()
			.Select(Parse);
	}

	private static Issue Parse(XmlElement xmlElement)
	{
		var dic = xmlElement.ChildNodes
			.Cast<XmlElement>()
			.ToDictionary(x => x.Name, x => x.InnerText);

		var rawTitle = dic["title"];
		string title;
		var resolved = true;

		var resolvedTokenIndex = rawTitle.IndexOf(ResolvedToken, StringComparison.InvariantCultureIgnoreCase);
		if (resolvedTokenIndex == -1)
		{
			title = rawTitle;
			resolved = false;
		}
		else
		{
			title = rawTitle[(resolvedTokenIndex + ResolvedToken.Length)..].Trim();
		}

		return new Issue(title, resolved);
	}
}
