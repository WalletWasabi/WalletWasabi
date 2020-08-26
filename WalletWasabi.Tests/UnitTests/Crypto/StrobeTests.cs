using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using WalletWasabi.Crypto;
using WalletWasabi.JsonConverters;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class Strobe128Tests
	{
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void BehaiorIsDeterministic()
		{
			var strobe1 = new Strobe128("TestProtocol.v0.0.1");
			var strobe2 = new Strobe128("TestProtocol.v0.0.1");

			strobe1.MetaAssociatedData(ToBytes("wabisabi://unitests/"), false);
			strobe1.MetaAssociatedData(ToBytes("greeting/"), true);
			strobe1.AssociatedData(ToBytes("hello"), false);

			strobe2.MetaAssociatedData(ToBytes("wabisabi://unitests/"), false);
			strobe2.MetaAssociatedData(ToBytes("greeting/"), true);
			strobe2.AssociatedData(ToBytes("hello"), false);

			strobe1.MetaAssociatedData(ToBytes("prf"), false);
			var rnd1 = strobe1.Prf(12, false);

			strobe2.MetaAssociatedData(ToBytes("prf"), false);
			var rnd2 = strobe2.Prf(12, false);

			Assert.Equal(rnd1, rnd2);

			strobe1.MetaAssociatedData(ToBytes("key"), false);
			strobe1.Key(rnd1, false);

			strobe2.MetaAssociatedData(ToBytes("key"), false);
			strobe2.Key(rnd2, false);

			strobe1.MetaAssociatedData(ToBytes("prf"), false);
			rnd1 = strobe1.Prf(12, false);

			strobe2.MetaAssociatedData(ToBytes("prf"), false);
			rnd2 = strobe2.Prf(12, false);

			Assert.Equal(rnd1, rnd2);
		}

		[Theory]
		[MemberData(nameof(GetStrobeTestVectors))]
		public void TestVectors(TestVector vector)
		{
			var init = vector.Operations.First();
			var strobe = new Strobe128(init.CustomString);

			Assert.Equal(init.StateAfter, strobe.ToString(), ignoreCase: true);

			foreach (var operation in vector.Operations.Skip(1).TakeWhile(x => new[] { "KEY", "AD", "PRF" }.Contains(x.Name) ))
			{
				switch(operation.Name)
				{
					case "KEY": strobe.Key(ByteHelpers.FromHex(operation.InputData), false); break;
					case "AD":
						if (operation.IsMeta)
						{
							 strobe.MetaAssociatedData(ByteHelpers.FromHex(operation.InputData), false);
						}
						else
						{
							 strobe.AssociatedData(ByteHelpers.FromHex(operation.InputData), false); 
						}
						break;
					case "PRF": 
						var output = strobe.Prf(operation.InputLength, false);
						Assert.Equal(operation.Output, ByteHelpers.ToHex(output), ignoreCase: true);
						break;
				}
				Assert.Equal(operation.StateAfter, strobe.ToString(), ignoreCase: true);
			}
		}

		public static IEnumerable<object[]> GetStrobeTestVectors()
		{
			var testVectorFile = File.ReadAllText("./UnitTests/Data/StrobeTestVectors.json");
			var x = JsonConvert.DeserializeObject<TestVectorsContainer>(testVectorFile);
			foreach (var vector in x.TestVectors.Where(x => !x.Name.Contains("streaming")))
			{
				yield return new object[] { vector };
			}
		}

		private static byte[] ToBytes(string s) => Encoding.UTF8.GetBytes(s);
		private static byte[] From(string s) => Encoding.UTF8.GetBytes(s);

	}

	public class TestVectorsContainer
	{
		[JsonProperty(PropertyName = "test_vectors")]
		public List<TestVector> TestVectors;
	}

	public class TestVector
	{
		[JsonProperty(PropertyName = "name")]
		public string Name;
		[JsonProperty(PropertyName = "operations")]
		public List<Operation> Operations;
	}

	public class Operation
	{
		[JsonProperty(PropertyName = "name")]
		public string Name;

		[JsonProperty(PropertyName = "security")]
		public int Security;
		
		[JsonProperty(PropertyName = "custom_string")]
		public string CustomString;
		
		[JsonProperty(PropertyName = "input_length")]
		public int InputLength;

		[JsonProperty(PropertyName = "output")]
		public string Output;

		[JsonProperty(PropertyName = "meta")]
		public bool IsMeta;
		
		[JsonProperty(PropertyName = "input_data")]
		public string InputData;
		
		[JsonProperty(PropertyName = "state_after")]
		public string StateAfter;
		
		[JsonProperty(PropertyName = "stream")]
		public bool IsStream;
	}
}
