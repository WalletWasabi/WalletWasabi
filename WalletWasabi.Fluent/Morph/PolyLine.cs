using System.Collections.ObjectModel;

namespace WalletWasabi.Fluent.Morph;

public class PolyLine
{
	public PolyLine()
	{
		XValues = new ObservableCollection<double>();
		YValues = new ObservableCollection<double>();
	}

	public PolyLine(ObservableCollection<double> xValues, ObservableCollection<double> yValues)
	{
		XValues = xValues;
		YValues = yValues;
	}

	public ObservableCollection<double> XValues { get; set; }
	public ObservableCollection<double> YValues { get; set; }

	public PolyLine Clone()
	{
			return new PolyLine(XValues, YValues);
	}
}
