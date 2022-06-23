using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WalletWasabi.Tor.NetworkChecker;

public class IssueParser : IIssueParser
{
	public Issue Parse(string str)
	{
		var node = ParseRoot(str);
		var dict = node.Children.ToDictionary(n => n.Name, n => n);

		var value = dict["date"].Value;
		return new Issue
		{
			Title = dict["title"].Value,
			Date = DateTimeOffset.Parse(value),
			Resolved = bool.Parse(dict["resolved"].Value),
			Affected = dict["affected"].Children.Select(x => x.Value).ToList(),
			Severity = dict["severity"].Value
		};
	}

	private static IEnumerable<Node> ParseYaml(string yaml)
	{
		using var reader = new StringReader(yaml);
		string? read;
		Node? lastNode = null;

		do
		{
			read = reader.ReadLine();

			if (read is null)
			{
				continue;
			}

			if (read.TrimStart().StartsWith("#"))
			{
				continue;
			}

			if (char.IsWhiteSpace(read.First()))
			{
				AddListItem(lastNode!, read);
			}
			else
			{
				var node = ParseLine(read);
				lastNode = node;
				yield return node;
			}
		}
		while (read is not null);
	}

	private static void AddListItem(Node lastNode, string line)
	{
		var hyphenIndex = line.IndexOf('-');
		var content = line[(hyphenIndex + 1)..].Trim();
		lastNode.Children.Add(new Node("", content));
	}

	private static Node ParseLine(string read)
	{
		var colonIndex = read.IndexOf(':');
		var name = read[..colonIndex].Trim();
		var value = read[(colonIndex + 1)..].Trim();
		return new Node(name, value);
	}

	private static (string yaml, string description) GetYaml(string input)
	{
		var regex = @"---\s+(.*)\s+---(.*)";
		var matches = Regex.Match(input, regex, RegexOptions.Singleline);
		var yaml = matches.Groups[1].Value;
		var description = matches.Groups[2].Value;

		return (yaml, description);
	}

	private static Node ParseRoot(string str)
	{
		var (yaml, description) = GetYaml(str);
		return new Node("Root", description, ParseYaml(yaml).ToList());
	}

	private class Node
	{
		public Node(string name, string value, ICollection<Node> children) : this(name, value)
		{
			Children = children;
		}

		public Node(string name, string value)
		{
			Name = name;
			Value = value;
		}

		public string Name { get; }
		public string Value { get; }
		public ICollection<Node> Children { get; } = new List<Node>();
	}
}
