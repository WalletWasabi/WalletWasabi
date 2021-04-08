using System;
using System.Collections.Generic;
using WalletWasabi.Fluent.MathNet;

namespace WalletWasabi.Fluent.ViewModels.LineChartDemo
{
	public class XYLineChartViewModel
	{
		public List<double> XAxisValues => new()
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

		public List<string> XAxisLabels => new List<string>()
		{
			"Jan," +
			"Feb," +
			"March",
			"April"
		};

		public List<double> YAxisValues => new()
		{
			0.2,
			0.81,
			2.23,
			2.12,
			1.8,
			1.33,
			3.33,
			4.44,
			4.21,
			4.01
		};
	}
}
