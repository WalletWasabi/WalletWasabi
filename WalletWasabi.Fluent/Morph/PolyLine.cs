using System.Collections.ObjectModel;

namespace WalletWasabi.Fluent.Morph
{
	public class PolyLine
	{
		public ObservableCollection<double> XValues { get; set; }
		public ObservableCollection<double> YValues { get; set; }

		public PolyLine Clone()
		{
			return new()
			{
				XValues = new ObservableCollection<double>(XValues),
				YValues = new ObservableCollection<double>(YValues)
			};
		}
	}
}