using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.ClientUpdates
{
	public class UpdateItem
	{
		public UpdateItem(DateTimeOffset date, string title, string description, Uri link)
		{
			Date = date;
			AssertTitleFormat(title);
			Title = title;
			AssertDescriptionFormat(description);
			Description = description;
			Link = link;
		}

		public DateTimeOffset Date { get; }
		public string Title { get; }
		public string Description { get; }
		public Uri Link { get; }

		private void AssertTitleFormat(string title)
		{
			AssertTextFormat(nameof(title), title, 3, 30);

			const int MaxWordCount = 3;
			if (title.Count(x => x == ' ') > MaxWordCount - 1)
			{
				throw new FormatException($"{nameof(title)} must contain max {MaxWordCount} words.");
			}

			if (title.Split(' ').Select(x => x.First()).Any(x => !char.IsLetter(x) || !char.IsUpper(x)))
			{
				throw new FormatException($"Every word in the {nameof(title)} must be capitalized.");
			}
		}

		private void AssertDescriptionFormat(string description)
		{
			AssertTextFormat(nameof(description), description, 50, 140);

			var firstLetter = description.First();
			if (!char.IsLetter(firstLetter) || !char.IsUpper(firstLetter))
			{
				throw new FormatException($"First letter of the {nameof(description)} must be capitalized.");
			}
		}

		private void AssertTextFormat(string name, string content, int minLength, int maxLength)
		{
			if (content.Trim().Length != content.Length)
			{
				throw new FormatException($"{name} must not contain whitespaces at the beginning and at the end.");
			}
			if (content.Any(x => x == '\n' || x == '\r'))
			{
				throw new FormatException($"{name} must not contain end line characters.");
			}
			if (content.Split("  ").Length != 1)
			{
				throw new FormatException($"{name} must not contain two spaces in a row.");
			}
			if (content.Length > maxLength)
			{
				throw new FormatException($"{name} must contain max {maxLength} characters.");
			}
			if (content.Length < minLength)
			{
				throw new FormatException($"{name} must contain min {minLength} characters.");
			}
		}
	}
}
