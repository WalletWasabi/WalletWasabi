using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.Graphs
{
	/// <summary>
	/// A connection between two UTXOs.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class CoinEdge : IEquatable<CoinEdge>, INotifyPropertyChanged
	{
		#region Events

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion Events

		#region Fields

		private SmartCoin[] _verticles;
		private double _weight;

		#endregion Fields

		#region Properties

		#region SerializableProperties

		/// <summary>
		/// There can be always only two verticles.
		/// </summary>
		[JsonProperty]
		public SmartCoin[] Verticles
		{
			get => _verticles;
			set
			{
				if (value != _verticles)
				{
					_verticles = value;
					OnPropertyChanged(nameof(Verticles));
				}
			}
		}

		/// <summary>
		/// It's always between 0 and 1.
		/// </summary>
		[JsonProperty]
		public double Weight
		{
			get => _weight;
			set
			{
				if (value != _weight)
				{
					_weight = value;
					OnPropertyChanged(nameof(Weight));
				}
			}
		}

		#endregion SerializableProperties

		#endregion Properties

		#region Constructors

		[JsonConstructor]
		public CoinEdge(SmartCoin[] verticles, double weight)
		{
			Create(verticles, weight);
		}

		public CoinEdge(SmartCoin vertex1, SmartCoin vertex2, double weight)
		{
			var verticles = new[] { vertex1, vertex2 };
			Create(verticles, weight);
		}

		private void Create(SmartCoin[] verticles, double weight)
		{
			Verticles = Guard.NotNullAndAssert(nameof(verticles), verticles, expectedCount: 2, expectUniqueness: true)
				.OrderBy(x => x.TransactionId.ToString()) // Assert Lexicographical order as defined in BIP126 (edges are undirectoed.)
				.ThenBy(x => x.Index)
				.ToArray();
			Weight = Guard.InRangeAndNotNull(nameof(weight), weight, 0, 1);

			// Add my reference to my verticles.
			foreach (SmartCoin verticle in Verticles)
			{
				verticle.Edges.Add(this);
			}
		}

		#endregion Constructors

		#region Methods

		// Remove my reference from my verticles.
		public void RemoveMe()
		{
			foreach (var verticle in Verticles)
			{
				verticle.Edges.Remove(this);
			}
		}

		#endregion Methods

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is CoinEdge && this == (CoinEdge)obj;

		public bool Equals(CoinEdge other) => this == other;

		public override int GetHashCode() => Verticles[0].GetHashCode() ^ Verticles[1].GetHashCode(); // Order is asserted in Create constructor.

		public static bool operator ==(CoinEdge x, CoinEdge y) => y?.Verticles[0] == x?.Verticles[0] && y?.Verticles[1] == x?.Verticles[1]; // Order is asserted in Create constructor.

		public static bool operator !=(CoinEdge x, CoinEdge y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
