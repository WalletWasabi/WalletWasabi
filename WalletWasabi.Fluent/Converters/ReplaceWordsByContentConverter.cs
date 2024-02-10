using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Metadata;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Converters;

/// <summary>
/// Replaces words in a string by arbitrary content using the dictionary provided in <see cref="Dictionary"/>
/// </summary>
public class ReplaceWordsByContentConverter : AvaloniaObject, IValueConverter
{
	public static readonly StyledProperty<ResourceDictionary?> DictionaryProperty = AvaloniaProperty.Register<ReplaceWordsByContentConverter, ResourceDictionary?>(nameof(Dictionary));

	[Content]
	public ResourceDictionary? Dictionary
	{
		get => GetValue(DictionaryProperty);
		set => SetValue(DictionaryProperty, value);
	}

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is string s)
		{
			return GenerateContent(s);
		}

		return value;
	}

	private Control GenerateContent(string s)
	{
		var words = s.Split(' ');

		var inlines = words
			.Select(InlineForWord)
			.Delimit(new Run(" "))
			.ToList();

		var inlineCollection = new InlineCollection();
		inlineCollection.AddRange(inlines);
		return new TextBlock { Inlines = inlineCollection };
	}

	private Inline InlineForWord(string word)
	{
		if (!Dictionary!.ContainsKey(word))
		{
			return new Run(word);
		}

		var dictItem = Dictionary[word];
		if (dictItem is Geometry c)
		{
			return new InlineUIContainer(new PathIcon() { Data = c });
		}

		Logger.LogError($"Object of invalid type found for key {word} in {nameof(ReplaceWordsByContentConverter)}");
		return new Run(word);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
