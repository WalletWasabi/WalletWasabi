using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.Converters;

public class TextToInlineCollectionConverter
{
	public static readonly FuncValueConverter<string, InlineCollection> Instance = new(ToInlineCollection);

	// This regular expression will handle detecting URLs and **bold** tags.
	private static readonly Regex Regex = new(@"(\*\*(.*?)\*\*)|(\b(?:https?://|www\.)\S+\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

	private static InlineCollection ToInlineCollection(string? str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return new InlineCollection { new Run { Text = string.Empty } }; // Returns an empty Run if there's no text.
		}

		var inlines = new List<Inline>();
		var lastIndex = 0;

		foreach (Match match in Regex.Matches(str))
		{
			// Add normal text before the found match.
			if (match.Index > lastIndex)
			{
				inlines.Add(new Run { Text = str[lastIndex..match.Index], BaselineAlignment = BaselineAlignment.Center });
			}

			// Check if the match is a URL or bold text.
			if (match.Value.StartsWith("**") && match.Value.EndsWith("**"))
			{
				// Extract the text inside the **bold** tags.
				var boldText = match.Groups[2].Value; // Uses the captured group for the bold text.
				inlines.Add(new Run { Text = boldText, FontWeight = FontWeight.Bold });
			}
			else if (!string.IsNullOrEmpty(match.Groups[3].Value)) // It's a URL.
			{
				var url = match.Groups[3].Value;
				var hyperlink = new InlineUIContainer(
					new ContentControl
					{
						Content = new LinkViewModel(UiContext.Default)
						{
							IsClickable = true,
							Description = url,
							Link = url
						}
					});

				hyperlink.BaselineAlignment = BaselineAlignment.Center;
				inlines.Add(hyperlink);
			}

			lastIndex = match.Index + match.Length;
		}

		// Add any remaining text after the last match.
		if (lastIndex < str.Length)
		{
			inlines.Add(new Run { Text = str[lastIndex..], BaselineAlignment = BaselineAlignment.Center });
		}

		var inlineCollection = new InlineCollection();
		inlineCollection.AddRange(inlines);
		return inlineCollection;
	}
}
