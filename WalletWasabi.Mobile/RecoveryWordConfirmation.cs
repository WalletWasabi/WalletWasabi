using NBitcoin;
using System.Security.Cryptography;

namespace WalletWasabi.Mobile;

public sealed record RecoveryWordQuestion(int Position, IReadOnlyList<string> Choices);

public sealed class RecoveryWordConfirmation
{
	private readonly Dictionary<int, string> _answers = new();
	private readonly HashSet<int> _confirmed = new();

	public RecoveryWordConfirmation(Mnemonic mnemonic)
	{
		var positions = new HashSet<int>();
		while (positions.Count < 3) { positions.Add(RandomNumberGenerator.GetInt32(mnemonic.Words.Length)); }
		var dictionary = mnemonic.WordList.GetWords();
		var recoveryWords = mnemonic.Words.ToHashSet(StringComparer.Ordinal);
		var questions = new List<RecoveryWordQuestion>();
		foreach (var index in positions.Order())
		{
			var correct = mnemonic.Words[index];
			var choices = new HashSet<string>(StringComparer.Ordinal) { correct };
			while (choices.Count < 6)
			{
				var word = dictionary[RandomNumberGenerator.GetInt32(dictionary.Count)];
				if (!recoveryWords.Contains(word)) { choices.Add(word); }
			}
			var shuffled = choices.ToArray();
			for (var i = shuffled.Length - 1; i > 0; i--)
			{
				var j = RandomNumberGenerator.GetInt32(i + 1);
				(shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
			}
			var position = index + 1;
			_answers.Add(position, correct);
			questions.Add(new RecoveryWordQuestion(position, Array.AsReadOnly(shuffled)));
		}
		Questions = questions.AsReadOnly();
	}

	public IReadOnlyList<RecoveryWordQuestion> Questions { get; }
	public bool IsComplete => _confirmed.Count == Questions.Count;

	public void ResetFrom(int questionIndex)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(questionIndex);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(questionIndex, Questions.Count);
		foreach (var question in Questions.Skip(questionIndex)) { _confirmed.Remove(question.Position); }
	}

	public bool Select(int position, string word)
	{
		var question = Questions.FirstOrDefault(q => q.Position == position);
		if (question is null || !question.Choices.Contains(word, StringComparer.Ordinal))
		{
			throw new ArgumentException("Select one of the displayed recovery words.");
		}
		if (word != _answers[position]) { _confirmed.Remove(position); return false; }
		_confirmed.Add(position);
		return true;
	}
}
