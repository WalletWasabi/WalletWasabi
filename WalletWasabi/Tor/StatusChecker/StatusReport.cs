using System.Xml.Serialization;

namespace WalletWasabi.Tor.StatusChecker;

[XmlRoot(ElementName = "item", Namespace = "", IsNullable = false)]
public class StatusReport
{
	[XmlElement("title")]
	public string Title { get; set; } = "";

	[XmlElement("link")]
	public string Link { get; set; } = "";

	[XmlElement("pubDate")]
	public string PubDate { get; set; } = "";

	[XmlElement("guid")]
	public string GUID { get; set; } = "";

	[XmlElement("category")]
	public string Category { get; set; } = "";

	[XmlElement("description")]
	public string Description { get; set; } = "";
}
