using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Helpers
{
	public static class Guard
	{
		public static T NotNull<T>(string parameterName, T value)
		{
			AssertCorrectParameterName(parameterName);

			if (value == null)
			{
				throw new ArgumentNullException(parameterName, "Parameter cannot be null.");
			}

			return value;
		}

		private static void AssertCorrectParameterName(string parameterName)
		{
			if (parameterName is null)
			{
				throw new ArgumentNullException(nameof(parameterName), "Parameter cannot be null.");
			}

			if (parameterName == "")
			{
				throw new ArgumentException("Parameter cannot be empty.", nameof(parameterName));
			}

			if (parameterName.Trim() == "")
			{
				throw new ArgumentException("Parameter cannot be whitespace.", nameof(parameterName));
			}
		}

		public static T Same<T>(string parameterName, T expected, T actual)
		{
			AssertCorrectParameterName(parameterName);
			NotNull(nameof(expected), expected);

			if (!expected.Equals(actual))
			{
				throw new ArgumentException($"Parameter must be {expected}. Actual: {actual}.", parameterName);
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

		public static Dictionary<TKey, TValue> NotNullOrEmpty<TKey, TValue>(string parameterName, Dictionary<TKey, TValue> value)
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
			if (trimmedValue == "")
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

		public static T MaximumAndNotNull<T>(string parameterName, T value, T greatest) where T : IComparable
		{
			NotNull(parameterName, value);

			if (value.CompareTo(greatest) > 0)
			{
				throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter cannot be greater than {greatest}.");
			}

			return value;
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
		public static string Correct(string str)
		{
			return string.IsNullOrWhiteSpace(str)
				? string.Empty
				: str.Trim();
		}
	}
}
