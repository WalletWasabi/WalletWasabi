using System;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.DataEncodation
{
	public sealed class ECISet
	{
		private Dictionary<string, int> _nameToValue;
		private Dictionary<int, string> _valueToName;

		public enum AppendOption { NameToValue, ValueToName, Both }

		private void AppendECI(string name, int value, AppendOption option)
		{
			switch (option)
			{
				case AppendOption.NameToValue:
					_nameToValue.Add(name, value);
					break;

				case AppendOption.ValueToName:
					_valueToName.Add(value, name);
					break;

				case AppendOption.Both:
					_nameToValue.Add(name, value);
					_valueToName.Add(value, name);
					break;

				default:
					throw new InvalidOperationException("There is no such AppendOption");
			}
		}

		/// <summary>
		/// Initialize ECI Set.
		/// </summary>
		/// <param name="option">AppendOption is enum under ECISet
		/// Use NameToValue during Encode. ValueToName during Decode</param>
		internal ECISet(AppendOption option)
		{
			Initialize(option);
		}

		private void Initialize(AppendOption option)
		{
			switch (option)
			{
				case AppendOption.NameToValue:
					_nameToValue = new Dictionary<string, int>();
					break;

				case AppendOption.ValueToName:
					_valueToName = new Dictionary<int, string>();
					break;

				case AppendOption.Both:
					_nameToValue = new Dictionary<string, int>();
					_valueToName = new Dictionary<int, string>();
					break;

				default:
					throw new InvalidOperationException("There is no such AppendOption");
			}

			//ECI table. Source 01 URL: http://strokescribe.com/en/ECI.html
			//ECI table. Source 02 URL: http://lab.must.or.kr/Extended-Channel-Interpretations-ECI-Encoding.ashx
			//ToDo. Fill up remaining missing table.
			AppendECI("iso-8859-1", 1, option);
			AppendECI("IBM437", 2, option);
			//AppendECI("iso-8859-1", 3, option);	//ECI value 1 is default encoding.
			AppendECI("iso-8859-2", 4, option);
			AppendECI("iso-8859-3", 5, option);
			AppendECI("iso-8859-4", 6, option);
			AppendECI("iso-8859-5", 7, option);
			AppendECI("iso-8859-6", 8, option);
			AppendECI("iso-8859-7", 9, option);
			AppendECI("iso-8859-8", 10, option);
			AppendECI("iso-8859-9", 11, option);
			AppendECI("windows-874", 13, option);
			AppendECI("iso-8859-13", 15, option);
			AppendECI("iso-8859-15", 17, option);
			AppendECI("shift_jis", 20, option);
			AppendECI("utf-8", 26, option);
		}

		internal int GetECIValueByName(string encodingName)
		{
			if (_nameToValue is null)
			{
				Initialize(AppendOption.NameToValue);
			}

			if (_nameToValue.TryGetValue(encodingName, out int ECIValue))
			{
				return ECIValue;
			}
			else
			{
				throw new ArgumentOutOfRangeException($"ECI doesn't contain encoding: {encodingName}");
			}
		}

		internal string GetECINameByValue(int ECIValue)
		{
			if (_valueToName is null)
			{
				Initialize(AppendOption.ValueToName);
			}

			if (_valueToName.TryGetValue(ECIValue, out string ECIName))
			{
				return ECIName;
			}
			else
			{
				throw new ArgumentOutOfRangeException($"ECI doesn't contain value: {ECIValue}");
			}
		}

		/// <remarks>ISO/IEC 18004:2006E ECI Designator Page 24</remarks>
		/// <param name="ECIValue">Range: 0 ~ 999999</param>
		/// <returns>Number of Codewords(Byte) for ECI Assignment Value</returns>
		private static int NumOfCodewords(int ECIValue)
		{
			if (ECIValue >= 0 && ECIValue <= 127)
			{
				return 1;
			}
			else if (ECIValue > 127 && ECIValue <= 16383)
			{
				return 2;
			}
			else if (ECIValue > 16383 && ECIValue <= 999999)
			{
				return 3;
			}
			else
			{
				throw new ArgumentOutOfRangeException("ECIValue should be in range: 0 to 999999");
			}
		}

		/// <remarks>ISO/IEC 18004:2006E ECI Designator Page 24</remarks>
		/// <param name="ECIValue">Range: 0 ~ 999999</param>
		/// <returns>Number of bits for ECI Assignment Value</returns>
		private static int NumOfAssignmentBits(int ECIValue) => NumOfCodewords(ECIValue) * 8;

		/// <remarks>ISO/IEC 18004:2006E ECI Designator Page 24</remarks>
		/// <param name="ECIValue">Range: 0 ~ 999999</param>
		/// <returns>Number of bits for ECI Header</returns>
		internal static int NumOfECIHeaderBits(int ECIValue) => NumOfAssignmentBits(ECIValue) + 4;

		/// <returns>ECI table in Dictionary collection</returns>
		public Dictionary<string, int> GetECITable()
		{
			if (_nameToValue is null)
			{
				Initialize(AppendOption.NameToValue);
			}

			return _nameToValue;
		}

		public bool ContainsECIName(string encodingName)
		{
			if (_nameToValue is null)
			{
				Initialize(AppendOption.NameToValue);
			}

			return _nameToValue.ContainsKey(encodingName);
		}

		public bool ContainsECIValue(int eciValue)
		{
			if (_valueToName is null)
			{
				Initialize(AppendOption.ValueToName);
			}

			return _valueToName.ContainsKey(eciValue);
		}

		/// <summary>
		/// ISO/IEC 18004:2006 Chapter 6.4.2 Mode indicator = 0111 Page 23
		/// </summary>
		private const int ECIMode = 7;

		private const int ECIIndicatorNumBits = 4;

		/// <summary>
		/// Length indicator for number of ECI codewords
		/// </summary>
		/// <remarks>ISO/IEC 18004:2006 Chapter 6.4.2 Page 24.
		/// 1 codeword length = 0. Any additional codeword add 1 to front. Eg: 3 = 110</remarks>
		/// <description>Bits required for each one is:
		/// one = 1, two = 2, three = 3</description>
		private enum ECICodewordsLength { One = 0, Two = 2, Three = 6 }

		/// <remarks>ISO/IEC 18004:2006 Chapter 6.4.2 Page 24.</remarks>
		internal BitList GetECIHeader(string encodingName)
		{
			int eciValue = GetECIValueByName(encodingName);

			BitList dataBits = new BitList
			{
				{ ECIMode, ECIIndicatorNumBits }
			};

			int eciAssignmentByte = NumOfCodewords(eciValue);
			//Number of bits = Num codewords indicator + codeword value = Number of codewords * 8
			//Chapter 6.4.2.1 ECI Designator ISOIEC 18004:2006 Page 24
			int eciAssignmentBits;
			switch (eciAssignmentByte)
			{
				case 1:
					//Indicator = 0. Page 24. Chapter 6.4.2.1
					dataBits.Add((int)ECICodewordsLength.One, 1);
					eciAssignmentBits = (eciAssignmentByte * 8) - 1;
					break;

				case 2:
					//Indicator = 10. Page 24. Chapter 6.4.2.1
					dataBits.Add((int)ECICodewordsLength.Two, 2);
					eciAssignmentBits = (eciAssignmentByte * 8) - 2;
					break;

				case 3:
					//Indicator = 110. Page 24. Chapter 6.4.2.1
					dataBits.Add((int)ECICodewordsLength.Three, 3);
					eciAssignmentBits = (eciAssignmentByte * 8) - 3;
					break;

				default:
					throw new InvalidOperationException("Assignment Codewords should be either 1, 2 or 3");
			}

			dataBits.Add(eciValue, eciAssignmentBits);

			return dataBits;
		}
	}
}
