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
			if (parameterName == null)
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
				throw new ArgumentException($"`Parameter must be {expected}. Actual: {actual}.", parameterName);
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

		public static int MinimumAndNotNull(string parameterName, int? value, int smallest)
		{
			NotNull(parameterName, value);

			if (value < smallest)
			{
				throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter cannot be less than {smallest}.");
			}

			return (int)value;
		}

		public static int MaximumAndNotNull(string parameterName, int? value, int greatest)
		{
			NotNull(parameterName, value);

			if (value > greatest)
			{
				throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter cannot be greater than {greatest}.");
			}

			return (int)value;
		}

		public static T InRangeAndNotNull<T>(string parameterName, T value, T smallest, T greatest) where T : IComparable
		{
			NotNull(parameterName, value);
			if (value.CompareTo(smallest) < 0)
				throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter cannot be less than {smallest}.");

			if (value.CompareTo(greatest) > 0)
				throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter cannot be greater than {greatest}.");

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

		public static void ThrowIf(bool condition, Type exceptionType, params object[] exceptionArguments)
		{
			if (condition)
			{
				var exceptionToThrow = Activator.CreateInstance(exceptionType, exceptionArguments);
				throw (Exception) exceptionToThrow;
			}
		}
	}
}
