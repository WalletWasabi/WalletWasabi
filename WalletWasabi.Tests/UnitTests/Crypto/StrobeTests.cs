using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto;
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

			strobe1.AddAssociatedMetaData(ToBytes("wabisabi://unitests/"), false);
			strobe1.AddAssociatedMetaData(ToBytes("greeting/"), true);
			strobe1.AddAssociatedData(ToBytes("hello"), false);

			strobe2.AddAssociatedMetaData(ToBytes("wabisabi://unitests/"), false);
			strobe2.AddAssociatedMetaData(ToBytes("greeting/"), true);
			strobe2.AddAssociatedData(ToBytes("hello"), false);

			strobe1.AddAssociatedMetaData(ToBytes("prf"), false);
			var rnd1 = strobe1.Prf(12, false);

			strobe2.AddAssociatedMetaData(ToBytes("prf"), false);
			var rnd2 = strobe2.Prf(12, false);

			Assert.Equal(rnd1, rnd2);

			strobe1.AddAssociatedMetaData(ToBytes("key"), false);
			strobe1.Key(rnd1, false);

			strobe2.AddAssociatedMetaData(ToBytes("key"), false);
			strobe2.Key(rnd2, false);

			strobe1.AddAssociatedMetaData(ToBytes("prf"), false);
			rnd1 = strobe1.Prf(12, false);

			strobe2.AddAssociatedMetaData(ToBytes("prf"), false);
			rnd2 = strobe2.Prf(12, false);

			Assert.Equal(rnd1, rnd2);

			var strobe3 = strobe1.MakeCopy();

			Assert.Equal(strobe1.ToString(), strobe3.ToString());

			strobe1.AddAssociatedData(ToBytes("Something"), false);

			Assert.NotEqual(strobe1.ToString(), strobe3.ToString());
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
							 strobe.AddAssociatedMetaData(ByteHelpers.FromHex(operation.InputData), false);
						}
						else
						{
							 strobe.AddAssociatedData(ByteHelpers.FromHex(operation.InputData), false); 
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
			var testSet = JsonConvert.DeserializeObject<StrobeTestSet>(testVectorFile);

			// return all test vectors except the one for streaming because it is not implemented.
			foreach (var vector in testSet.TestVectors.Where(x => !x.Name.Contains("streaming")))
			{
				yield return new object[] { vector };
			}
		}

		private static byte[] ToBytes(string s) => Encoding.UTF8.GetBytes(s);
	}

	public class StrobeTestSet
	{
		[JsonProperty(PropertyName = "test_vectors")]
		public List<TestVector> TestVectors { get; }

		[JsonConstructor]
		public StrobeTestSet(List<TestVector> testVectors)
		{
			TestVectors = testVectors;
		}
	}

	public class TestVector
	{
		[JsonProperty(PropertyName = "name")]
		public string Name { get; }
		[JsonProperty(PropertyName = "operations")]
		public List<Operation> Operations  { get; }

		[JsonConstructor]
		public TestVector(string name, List<Operation> operations)
		{
			Name = name;
			Operations = operations;
		}
	}

	public class Operation
	{
		[JsonProperty(PropertyName = "name")]
		public string Name { get; }

		[JsonProperty(PropertyName = "security")]
		public int Security { get; }
		
		[JsonProperty(PropertyName = "custom_string")]
		public string CustomString { get; }
		
		[JsonProperty(PropertyName = "input_length")]
		public int InputLength { get; }

		[JsonProperty(PropertyName = "output")]
		public string Output { get; }

		[JsonProperty(PropertyName = "meta")]
		public bool IsMeta { get; }
		
		[JsonProperty(PropertyName = "input_data")]
		public string InputData { get; }
		
		[JsonProperty(PropertyName = "state_after")]
		public string StateAfter { get; }
		
		[JsonProperty(PropertyName = "stream")]
		public bool IsStream { get; }

		[JsonConstructor]
		public Operation(string name, int security, string customString, int inputLength, string output, bool isMeta, string inputData, string stateAfter, bool isStream)
		{
			Name = name;
			Security = security;
			CustomString = customString;
			InputLength = inputLength;
			Output = output;
			IsMeta = isMeta;
			InputData = inputData;
			StateAfter = stateAfter;
			IsStream = isStream; 
		}		
	}
}
