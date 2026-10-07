namespace WalletWasabi.Mobile;

public static class WalletNameSuggestion
{
	private static readonly string[] Numbers = ["Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"];
	private static readonly string[] Ordinals = ["", "First", "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh", "Eighth", "Ninth", "Tenth", "Eleventh", "Twelfth", "Thirteenth", "Fourteenth", "Fifteenth", "Sixteenth", "Seventeenth", "Eighteenth", "Nineteenth"];
	private static readonly string[] Tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];
	private static readonly (int Value, string Name)[] Scales = [(1_000_000_000, "Billion"), (1_000_000, "Million"), (1_000, "Thousand"), (100, "Hundred")];

	public static string Next(IEnumerable<string> existingNames)
	{
		var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
		for (var number = 1; ; number++)
		{
			var name = Ordinal(number) + " Wallet";
			if (!existing.Contains(name)) { return name; }
		}
	}

	private static string Ordinal(int number)
	{
		if (number < 20) { return Ordinals[number]; }
		if (number < 100)
		{
			return number % 10 == 0 ? Tens[number / 10][..^1] + "ieth" : Tens[number / 10] + "-" + Ordinals[number % 10];
		}
		var scale = Scales.First(s => number >= s.Value);
		return Cardinal(number / scale.Value) + " " + scale.Name + (number % scale.Value == 0 ? "th" : " " + Ordinal(number % scale.Value));
	}

	private static string Cardinal(int number)
	{
		if (number < 20) { return Numbers[number]; }
		if (number < 100) { return Tens[number / 10] + (number % 10 == 0 ? "" : "-" + Numbers[number % 10]); }
		var scale = Scales.First(s => number >= s.Value);
		return Cardinal(number / scale.Value) + " " + scale.Name + (number % scale.Value == 0 ? "" : " " + Cardinal(number % scale.Value));
	}
}
