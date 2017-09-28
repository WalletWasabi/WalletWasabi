using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
using HBitcoin.FullBlockSpv;
using HBitcoin.KeyManagement;
using HBitcoin.Models;
using NBitcoin;
using Xunit;

namespace HBitcoin.Tests
{
    public class MiscTests
    {
	    [Fact]
	    public void SmartTransactionHashSetTest()
	    {
		    Tracker tracker = new Tracker(Network.Main);
		    var tx1 = new Transaction(
			    "0100000003192a9e09a4eb4829dd92e437d50a6d544626a0c0a4345fe85754cf0b509c56da090000006a47304402205ae8feded01cff3dafa698481a0e941a5059b9de234e794335493f863da6b8b702207645e57ec428b553e57e30115eaa3eb8828069e7933e09e4c867d086de2d67290121022f4cae09039d26ee9481ac235d0c74a1c1cdb1780acbb4300ce5a14d2c7a69e0ffffffff05d2352f130dac517aba5e6fdf966d915b15fbf8c674a9809d7706fd9bc2c545290000006a4730440220771193a38f8e05e59dc8233b46e181c9a75808c0ec1efeeedfebe5c7018d2c1f02206c32485d4fab65d47d7d4392d937128af44e918bd0fdded4d0b82b0a9d316dcc0121028fc5d9cd68ba58123ff972bb73682019cd1ee395f92c0b72e73ebc7151e3fe43ffffffff1cb828c3dcb9abbf77d63dbc9a050c70a05c5d0f2988da89811803f7dee4f090050000006b483045022100e32f6195c4437c4a3f0de88157989830c6c0effcecd093324b2dd33b81b9743902203e9bf7f36dc0a879de165c96b137ca163c3506fc5ac9db377650632e1e922e4f01210358e1623b2ffdf9b0bc135026a5e8fdd1625f61424937b7c1cb6e47af1f06bd0affffffff026ba91a00000000001976a914cc17bf00ffba667e4f8ff425500b83f3a863922788acf60b0300000000001976a914f73f6ea0a99cbcc985a0dae57b9938e0dfc245c088ac00000000");
		    var tx2 =
			    new Transaction(
				    "0100000001c3dd09422c44e8cab3065aac65f1cc7befce2a0ea5d309d8d213ec08877dbd4b000000006a473044022001ab7769a0d735bbe4756260c30ab8127f4282cdd00b66991ca6bfc383b5005c02203d29981127717946bf05fc15177f3042aa0b995db786a868de4cc67bfd88801d0121037df55814a04730433b1ee3bcc8f089eef346e556d222b13f1a57a355fd8d07f6ffffffff0120300500000000001976a914f55586866f28d6db41529b8bd85db09e88221a1388ac00000000");
		    Assert.True(tracker.TrackedTransactions.TryAdd(new SmartTransaction(tx1, new Height(1))));
			Assert.True(tracker.TrackedTransactions.TryAdd(new SmartTransaction(tx2, new Height(1))));
			Assert.False(tracker.TrackedTransactions.TryAdd(new SmartTransaction(tx2, new Height(1))));
		    
		    HashSet<SmartTransaction> stx = new HashSet<SmartTransaction>();
			Assert.True(stx.Add(new SmartTransaction(tx1, new Height(1))));
			Assert.True(stx.Add(new SmartTransaction(tx2, new Height(2))));
			Assert.False(stx.Add(new SmartTransaction(tx2, new Height(3))));

			ConcurrentHashSet<SmartTransaction> stxchs = new ConcurrentHashSet<SmartTransaction>();
			Assert.True(stxchs.Add(new SmartTransaction(tx1, new Height(1))));
			Assert.True(stxchs.Add(new SmartTransaction(tx2, new Height(2))));
			Assert.False(stxchs.Add(new SmartTransaction(tx2, new Height(3))));

			ConcurrentObservableHashSet<SmartTransaction> stxcohs = new ConcurrentObservableHashSet<SmartTransaction>();
			Assert.True(stxcohs.TryAdd(new SmartTransaction(tx1, new Height(1))));
			Assert.True(stxcohs.TryAdd(new SmartTransaction(tx2, new Height(2))));
			Assert.False(stxcohs.TryAdd(new SmartTransaction(tx2, new Height(3))));
		}

	    [Fact]
		public void ConcurrentObservableDictionaryTest()
		{
			ConcurrentObservableDictionary<int, string> dict = new ConcurrentObservableDictionary<int, string>();
			var times = 0;
			dict.CollectionChanged += delegate
			{
				times++;
			};

			dict.Add(1, "foo");
			dict.Add(2, "moo");
			dict.Add(3, "boo");

			dict.AddOrReplace(1, "boo");
			dict.Remove(dict.First(x => x.Value == "moo"));

			Assert.True(dict.Values.All(x => x == "boo"));
			Assert.Equal(5, times);
		}

		[Fact]
		public void ConcurrentObservableHashSetTest()
		{
			ConcurrentObservableHashSet<string> hashSet = new ConcurrentObservableHashSet<string>();
			var times = 0;
			hashSet.CollectionChanged += delegate
			{
				times++;
			};

			hashSet.Clear(); // no fire
			hashSet.TryAdd("foo"); // fire
			hashSet.TryAdd("foo"); // no fire
			hashSet.TryAdd("moo"); // fire
			hashSet.TryRemove("foo"); // fire
			hashSet.Clear(); // fire
			
			Assert.Equal(4, times);
		}
	}
}
