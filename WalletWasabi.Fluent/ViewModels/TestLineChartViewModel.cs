using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels
{
    public class TestLineChartViewModel
    {
        public List<double> Values => new List<double>()
        {
            15,
            22,
            44,
            50,
            64,
            68,
            92,
            114,
            118,
            142,
            182,
            222,
            446,
            548,
            600
        };

        // public List<string> Labels => new List<string>()
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
            "6d",
            "4d",
            "3d",
            "1d",
            "22h",
            "20h",
            "18h",
            "10h",
            "6h",
            "4h",
            "2h",
            "1h",
            "50m",
            "30m",
            "20m"
        };

        public List<double> XAxisLabelsPosition => new List<double>()
        {
	        864,
	        576,
	        432,
	        144,
	        132,
	        120,
	        108,
	        60,
	        36,
	        24,
	        12,
	        6,
	        5,
	        3,
	        2,
        };

        public double XCurrentValue { get; set; } = 36;

        public double XMinValue { get; set; } = 2;

        public double XMaxValue { get; set; } = 864;
    }
}