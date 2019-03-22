using NBitcoin;
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

		private Script _scriptPubKeyConnection;

		private double _weight;

		#endregion Fields

		#region Properties

		/// <summary>
		/// There can be always only two verticles.
		/// </summary>
		public SmartCoin[] Verticles
		{
			get => _verticles;
			private set
			{
				if (value != _verticles)
				{
					_verticles = Guard.NotNullAndAssert(nameof(value), value, expectedCount: 2, expectUniqueness: true)
						.OrderBy(x => x.TransactionId.ToString()) // Assert Lexicographical order as defined in BIP126 (edges are undirectoed.)
						.ThenBy(x => x.Index)
						.ToArray();
					OnPropertyChanged(nameof(Verticles));
				}
			}
		}

		public Script ScriptPubKeyConnection
		{
			get => _scriptPubKeyConnection;
			set
			{
				if (value != _scriptPubKeyConnection)
				{
					_scriptPubKeyConnection = value;
					OnPropertyChanged(nameof(Verticles));
					RecalculateWeight();
				}
			}
		}

		/// <summary>
		/// It's always between 0 and 1.
		/// </summary>
		public double Weight
		{
			get => _weight;
			private set
			{
				if (value != _weight)
				{
					_weight = Guard.InRangeAndNotNull(nameof(value), value, 0, 1);
					OnPropertyChanged(nameof(Weight));
				}
			}
		}

		#endregion Properties

		#region Constructors

		private CoinEdge(SmartCoin vertex1, SmartCoin vertex2)
		{
			Verticles = new[] { vertex1, vertex2 };
			ScriptPubKeyConnection = null;
			Weight = 0;
		}

		#endregion Constructors

		#region Methods

		private void RecalculateWeight()
		{
			if (_scriptPubKeyConnection != null) // If the scriptPubKey is the same then that's common ownership (ToDo: except when OP_return, etc...)
			{
				Weight = 1;
			}
		}

		#endregion Methods

		#region Statics

		private static object Lock { get; } = new object();

		public static void CreateOrUpdateIfScriptPubKeyConnection(SmartCoin vertex1, SmartCoin vertex2)
		{
			lock (Lock)
			{
				// Same address. ToDo: Should we rather check pubkey hashes here somehow?
				if (vertex1.ScriptPubKey != vertex2.ScriptPubKey)
				{
					return;
				}

				// Create a new edge and test if verticles have the edge.
				var newEdge = new CoinEdge(vertex1, vertex2);
				foreach (SmartCoin verticle in newEdge.Verticles)
				{
					// Add my reference to my verticles.
					if (!verticle.Edges.Add(newEdge)) // If couldn't add, then it's duplication, so just update existing instead of adding the new.
					{
						var sameEdge = verticle.Edges.FirstOrDefault(x => x == newEdge);
						if (sameEdge != default)
						{
							sameEdge.ScriptPubKeyConnection = verticle.ScriptPubKey;
						}
					}
					else
					{
						newEdge.ScriptPubKeyConnection = verticle.ScriptPubKey;
					}
				}
			}
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
