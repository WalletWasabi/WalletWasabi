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

		/// <summary>
		/// There can be always only two verticles.
		/// </summary>
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

		#endregion Properties

		#region Constructors

		private CoinEdge(SmartCoin[] verticles, double weight)
		{
			Verticles = Guard.NotNullAndAssert(nameof(verticles), verticles, expectedCount: 2, expectUniqueness: true)
				.OrderBy(x => x.TransactionId.ToString()) // Assert Lexicographical order as defined in BIP126 (edges are undirectoed.)
				.ThenBy(x => x.Index)
				.ToArray();
			Weight = Guard.InRangeAndNotNull(nameof(weight), weight, 0, 1);

			// Add my reference to my verticles.
			foreach (SmartCoin verticle in Verticles)
			{
				if (!verticle.Edges.Add(this)) // If couldn't add, then it's duplication, so update existing instead of add.
				{
					var sameEdge = verticle.Edges.FirstOrDefault(x => x == this);
					if (sameEdge != default)
					{
						sameEdge.Weight = Weight;
					}
				}
			}
		}

		#endregion Constructors

		#region Statics

		private static object Lock { get; } = new object();

		public static CoinEdge CreateOrUpdate(SmartCoin[] verticles, double weight)
		{
			lock (Lock)
			{
				return new CoinEdge(verticles, weight);
			}
		}

		public static CoinEdge CreateOrUpdate(SmartCoin vertex1, SmartCoin vertex2, double weight)
		{
			var verticles = new[] { vertex1, vertex2 };
			return CreateOrUpdate(verticles, weight);
		}

		// Remove references from its verticles.
		public static void Remove(CoinEdge edge)
		{
			lock (Lock)
			{
				foreach (var verticle in edge.Verticles)
				{
					verticle.Edges.Remove(edge);
				}
			}
		}

		#endregion Statics

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is CoinEdge && this == (CoinEdge)obj;

		public bool Equals(CoinEdge other) => this == other;

		public override int GetHashCode() => Verticles[0].GetHashCode() ^ Verticles[1].GetHashCode(); // Order is asserted in Create constructor.

		public static bool operator ==(CoinEdge x, CoinEdge y) => y?.Verticles[0] == x?.Verticles[0] && y?.Verticles[1] == x?.Verticles[1]; // Order is asserted in Create constructor.

		public static bool operator !=(CoinEdge x, CoinEdge y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
