using NBitcoin;
using WalletWasabi.Mobile;
using Xunit;

namespace WalletWasabi.Mobile.Tests;

public class RecoveryWordConfirmationTests
{
	private static readonly Mnemonic RepeatedWords = new("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");

	[Fact]
	public void RepeatedSeedWordsHaveOneCorrectChoiceAndRequireEveryPosition()
	{
		var confirmation = new RecoveryWordConfirmation(RepeatedWords);
		Assert.False(confirmation.IsComplete);
		Assert.Equal(3, confirmation.Questions.Select(q => q.Position).Distinct().Count());
		foreach (var question in confirmation.Questions)
		{
			var correct = RepeatedWords.Words[question.Position - 1];
			Assert.Equal(6, question.Choices.Distinct().Count());
			Assert.Single(question.Choices, word => word == correct);
			Assert.All(question.Choices, word => Assert.Contains(word, Wordlist.English.GetWords()));
			Assert.False(confirmation.Select(question.Position, question.Choices.First(word => word != correct)));
			Assert.False(confirmation.IsComplete);
			Assert.True(confirmation.Select(question.Position, correct));
		}
		Assert.True(confirmation.IsComplete);
	}

	[Fact]
	public void ARepeatedTapCannotConfirmOtherPositions()
	{
		var confirmation = new RecoveryWordConfirmation(RepeatedWords);
		var first = confirmation.Questions[0];
		for (var i = 0; i < 3; i++) { Assert.True(confirmation.Select(first.Position, RepeatedWords.Words[first.Position - 1])); }
		Assert.False(confirmation.IsComplete);
		Assert.Throws<ArgumentException>(() => confirmation.Select(first.Position, "not a recovery word"));
		Assert.Throws<ArgumentException>(() => confirmation.Select(0, first.Choices[0]));
		Assert.False(confirmation.IsComplete);
	}

	[Fact]
	public void GoingBackRevokesLaterConfirmations()
	{
		var confirmation = new RecoveryWordConfirmation(RepeatedWords);
		foreach (var question in confirmation.Questions) { confirmation.Select(question.Position, RepeatedWords.Words[question.Position - 1]); }
		Assert.True(confirmation.IsComplete);
		confirmation.ResetFrom(1);
		Assert.False(confirmation.IsComplete);
		var second = confirmation.Questions[1];
		confirmation.Select(second.Position, RepeatedWords.Words[second.Position - 1]);
		Assert.False(confirmation.IsComplete);
		var third = confirmation.Questions[2];
		confirmation.Select(third.Position, RepeatedWords.Words[third.Position - 1]);
		Assert.True(confirmation.IsComplete);
		Assert.False(confirmation.Select(third.Position, third.Choices.First(w => w != RepeatedWords.Words[third.Position - 1])));
		Assert.False(confirmation.IsComplete);
	}
}
