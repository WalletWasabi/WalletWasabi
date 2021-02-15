using System;
using System.Collections.Generic;
using WalletWasabi.Fluent.MathNet;

namespace WalletWasabi.Fluent.ViewModels.LineChartDemo
{
	public class XYLineChartViewModel
	{
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
