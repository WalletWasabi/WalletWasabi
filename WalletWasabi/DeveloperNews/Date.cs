using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.DeveloperNews
{
	public class Date : IEquatable<Date>
	{
		public Date(int year, int month, int day)
		{
			Year = year;
			Month = month;
			Day = day;
		}

		public int Year { get; }
		public int Month { get; }
		public int Day { get; }

		public static Date Parse(string dateString)
		{
			var parts = dateString.Split('-');
			return new Date(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
		}

		public override string ToString() => $"{Year}-{Month}-{Day}";

		public override bool Equals(object? obj) => Equals(obj as Date);

		public bool Equals(Date? other) => this == other;

		public override int GetHashCode() => (Year, Month, Day).GetHashCode();

		public static bool operator ==(Date? x, Date? y) => (x?.Year, x?.Month, x?.Day) == (y?.Year, y?.Month, y?.Day);

		public static bool operator !=(Date? x, Date? y) => !(x == y);
	}
}
