using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.News
{
	public class Date
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
	}
}
