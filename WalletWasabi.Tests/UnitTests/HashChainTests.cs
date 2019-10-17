using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Stores;
using Xunit;
using Xunit.Sdk;

namespace WalletWasabi.Tests.UnitTests
{
	public class HashChainTests
	{
		[Fact]
		public void GeneralHashChainTests()
		{
			var hashChain = new HashChain();

			// ASSERT PROPERTIES

			// Assert everything is default value.
			AssertEverythingDefault(hashChain);

			// ASSERT EVENTS

			// Assert some functions do not raise any events when default.
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.HashCount),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert RemoveLast does not modify nor throw anything when nothing is added.
						hashChain.RemoveLast();
						AssertEverythingDefault(hashChain);
					}));
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.HashesLeft),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert RemoveLast does not modify nor throw anything when nothing is added.
						hashChain.RemoveLast();
						AssertEverythingDefault(hashChain);
					}));
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.ServerTipHeight),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert RemoveLast does not modify nor throw anything when nothing is added.
						hashChain.RemoveLast();
						AssertEverythingDefault(hashChain);
					}));
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.TipHash),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert RemoveLast does not modify nor throw anything when nothing is added.
						hashChain.RemoveLast();
						AssertEverythingDefault(hashChain);
					}));
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.TipHeight),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert RemoveLast does not modify nor throw anything when nothing is added.
						hashChain.RemoveLast();
						AssertEverythingDefault(hashChain);
					}));

			// Assert the correct events are thrown and not thrown when applicable.
			var newServerHeight = hashChain.ServerTipHeight + 1;
			Assert.PropertyChanged(hashChain,
					nameof(hashChain.ServerTipHeight),
					// ASSERT FUNCTION
					// Assert update server height raises.
					() => hashChain.UpdateServerTipHeight(newServerHeight));
			newServerHeight++;
			Assert.PropertyChanged(hashChain,
					nameof(hashChain.HashesLeft),
					// ASSERT FUNCTION
					// Assert update server height raises.
					() => hashChain.UpdateServerTipHeight(newServerHeight));

			newServerHeight++;
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.HashCount),
					// ASSERT FUNCTIONS
					// Assert update server height does not raise unnecessary events.
					() => hashChain.UpdateServerTipHeight(newServerHeight)));
			newServerHeight++;
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.TipHash),
					// ASSERT FUNCTIONS
					// Assert update server height does not raise unnecessary events.
					() => hashChain.UpdateServerTipHeight(newServerHeight)));
			newServerHeight++;
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.TipHeight),
					// ASSERT FUNCTIONS
					// Assert update server height does not raise unnecessary events.
					() => hashChain.UpdateServerTipHeight(newServerHeight)));
			var sameServerheight = newServerHeight;
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.ServerTipHeight),
					// ASSERT FUNCTIONS
					// Assert update server height does not raise without actually changing.
					() => hashChain.UpdateServerTipHeight(sameServerheight)));
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.HashesLeft),
					// ASSERT FUNCTIONS
					// Assert update server height does not raise without actually changing.
					() => hashChain.UpdateServerTipHeight(sameServerheight)));

			// ASSERT PROPERTIES
			Assert.Equal(0, hashChain.HashCount);
			var hashesLeft = sameServerheight;
			Assert.Equal((int)hashesLeft, hashChain.HashesLeft);
			Assert.Equal(hashesLeft, hashChain.ServerTipHeight);
			Assert.Null(hashChain.TipHash);
			Assert.Equal(0u, hashChain.TipHeight);
		}

		private static void AssertEverythingDefault(HashChain hashChain)
		{
			Assert.Equal(0, hashChain.HashCount);
			Assert.Equal(0, hashChain.HashesLeft);
			Assert.Equal(0u, hashChain.ServerTipHeight);
			Assert.Null(hashChain.TipHash);
			Assert.Equal(0u, hashChain.TipHeight);
		}
	}
}
