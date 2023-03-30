using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.Helpers;

public static class Guard
{
	public static bool True(string parameterName, bool? value, string? description = null)
		=> AssertBool(parameterName, true, value, description);

	public static bool False(string parameterName, bool? value, string? description = null)
		=> AssertBool(parameterName, false, value, description);

	private static bool AssertBool(string parameterName, bool expectedValue, bool? value, string? description = null)
	{
		NotNull(parameterName, value);

		if (value != expectedValue)
		{
			throw new ArgumentOutOfRangeException(parameterName, value, description ?? $"Parameter must be {expectedValue}.");
		}

		return (bool)value;
	}

	[return: NotNull]
	public static T NotNull<T>(string parameterName, [NotNull] T? value)
	{
		AssertCorrectParameterName(parameterName);
		return value ?? throw new ArgumentNullException(parameterName, "Parameter cannot be null.");
	}

	private static void AssertCorrectParameterName(string parameterName)
	{
		if (parameterName is null)
		{
			throw new ArgumentNullException(nameof(parameterName), "Parameter cannot be null.");
		}

		if (parameterName.Length == 0)
		{
			throw new ArgumentException("Parameter cannot be empty.", nameof(parameterName));
		}

		if (parameterName.Trim().Length == 0)
		{
			throw new ArgumentException("Parameter cannot be whitespace.", nameof(parameterName));
		}
	}

	public static T Same<T>(string parameterName, T expected, T actual)
	{
		AssertCorrectParameterName(parameterName);
		T expected2 = NotNull(nameof(expected), expected);

		if (!expected2.Equals(actual))
		{
			throw new ArgumentException($"Parameter must be {expected2}. Actual: {actual}.", parameterName);
		}

		return actual;
	}

	public static IEnumerable<T> NotNullOrEmpty<T>(string parameterName, IEnumerable<T> value)
	{
		NotNull(parameterName, value);

		if (!value.Any())
		{
			throw new ArgumentException("Parameter cannot be empty.", parameterName);
		}

		return value;
	}

	public static T[] NotNullOrEmpty<T>(string parameterName, T[] value)
	{
		NotNull(parameterName, value);

		if (!value.Any())
		{
			throw new ArgumentException("Parameter cannot be empty.", parameterName);
		}

		return value;
	}

	public static IDictionary<TKey, TValue> NotNullOrEmpty<TKey, TValue>(string parameterName, IDictionary<TKey, TValue> value)
	{
		NotNull(parameterName, value);
		if (!value.Any())
		{
			throw new ArgumentException("Parameter cannot be empty.", parameterName);
		}
		return value;
	}

	public static string NotNullOrEmptyOrWhitespace(string parameterName, string value, bool trim = false)
	{
		NotNullOrEmpty(parameterName, value);

		string trimmedValue = value.Trim();
		if (trimmedValue.Length == 0)
		{
			throw new ArgumentException("Parameter cannot be whitespace.", parameterName);
		}

		if (trim)
		{
			return trimmedValue;
		}
		else
		{
			return value;
		}
	}

	public static T MinimumAndNotNull<T>(string parameterName, T value, T smallest) where T : IComparable
	{
		NotNull(parameterName, value);

		if (value.CompareTo(smallest) < 0)
		{
			throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter cannot be less than {smallest}.");
		}

		return value;
	}

	public static IEnumerable<T> InRange<T>(string containerName, IEnumerable<T> container, int minCount, int maxCount)
	{
		var count = container.Count();
		if (count < minCount || count > maxCount)
		{
			throw new ArgumentOutOfRangeException(containerName, count, $"{containerName}.Count() cannot be less than {minCount} or greater than {maxCount}.");
		}
		return container;
	}

	public static T InRangeAndNotNull<T>(string parameterName, T value, T smallest, T greatest) where T : IComparable
	{
		NotNull(parameterName, value);

		if (value.CompareTo(smallest) < 0)
		{
			throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter cannot be less than {smallest}.");
		}

		if (value.CompareTo(greatest) > 0)
		{
			throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter cannot be greater than {greatest}.");
		}

		return value;
	}

	/// <summary>
	/// Corrects the string:
	/// If the string is null, it'll be empty.
	/// Trims the string.
	/// </summary>
	[return: NotNull]
	public static string Correct(string? str)
	{
		return string.IsNullOrWhiteSpace(str)
			? ""
			: str.Trim();
	}
}
