using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WalletWasabi.Tor.NetworkChecker;

public class YamlDotNetIssueParser : IIssueParser
{
	private readonly IDeserializer _deserializer;

	public YamlDotNetIssueParser()
	{
		_deserializer = new DeserializerBuilder()
			.IgnoreUnmatchedProperties()
			.WithNamingConvention(CamelCaseNamingConvention.Instance)
			.Build();
	}

	public Issue Parse(string str)
	{
		var regex = @"---\s+(.*)\s+---(.*)";
		var matches = Regex.Match(str, regex, RegexOptions.Singleline);
		var yml = matches.Groups[1].Value;
		var issue = _deserializer.Deserialize<Issue>(yml);
		return issue;
	}
}
