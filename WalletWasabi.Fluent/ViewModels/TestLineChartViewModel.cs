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

        public List<string> Labels => new List<string>()
        {
            "6 days",
            "4 days",
            "3 days",
            "1 day",
            "22 hours",
            "20 hours",
            "18 hours",
            "10 hours",
            "6 hours",
            "4 hours",
            "2 hours",
            "1 hour",
            "50 min",
            "30 min",
            "20 min"
        };
    }
}