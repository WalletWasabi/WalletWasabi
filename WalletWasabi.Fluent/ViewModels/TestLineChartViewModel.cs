using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels
{
	public class TestLineChartViewModel
	{
		public double XAxisCurrentValue { get; set; } = 36;

		public double XAxisMinValue { get; set; } = 2;

		public double XAxisMaxValue { get; set; } = 864;

		// public List<string> XAxisLabels => new List<string>()
		// {
		//     "6 days",
		//     "4 days",
		//     "3 days",
		//     "1 day",
		//     "22 hours",
		//     "20 hours",
		//     "18 hours",
		//     "10 hours",
		//     "6 hours",
		//     "4 hours",
		//     "2 hours",
		//     "1 hour",
		//     "50 min",
		//     "30 min",
		//     "20 min"
		// };

		public List<string> XAxisLabels => new List<string>()
		{
			"1w",
			"3d",
			"1d",
			"12h",
			"6h",
			"3h",
			"1h",
			"30m",
			"20m",
			"fastest"
		};

		public List<double> XAxisValues => new List<double>()
		{
			1008,
			432,
			144,
			72,
			36,
			18,
			6,
			3,
			2,
			1,
		};

		public List<double> YAxisValues => new List<double>()
		{
			4,
			4,
			7,
			22,
			57,
			97,
			102,
			123,
			123,
			185
		};
	}
}
