using System;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.DataEncodation
{
	/// <summary>
	/// Table at chapter 8.4.3. P.21
	/// </summary>
	internal class AlphanumericTable
	{
		private static readonly Dictionary<char, int> s_AlphanumericTable = 
			new Dictionary<char, int>
		{
			{'0', 0},
			{'1', 1},
			{'2', 2},
			{'3', 3},
			{'4', 4},
			{'5', 5},
			{'6', 6},
			{'7', 7},
			{'8', 8},
			{'9', 9},
			{'A', 10},
			{'B', 11},
			{'C', 12},
			{'D', 13},
			{'E', 14},
			{'F', 15},
			{'G', 16},
			{'H', 17},
			{'I', 18},
			{'J', 19},
			{'K', 20},
			{'L', 21},
			{'M', 22},
			{'N', 23},
			{'O', 24},
			{'P', 25},
			{'Q', 26},
			{'R', 27},
			{'S', 28},
			{'T', 29},
			{'U', 30},
			{'V', 31},
			{'W', 32},
			{'X', 33},
			{'Y', 34},
			{'Z', 35},
			{'\x0020', 36},  //"SP"
			{'\x0024', 37},  //"$"
			{'\x0025', 38},  //"%" 
			{'\x002A', 39},  //"*"
			{'\x002B', 40},  //"+"
			{'\x002D', 41},  //"-"
			{'\x002E', 42},  //"."
			{'\x002F', 43}, //"/"
			{'\x003A', 44},	//":"
		};
		
		/// <summary>
		/// Convert char to int value
		/// </summary>
		/// <param name="inputChar">Alpha Numeric Char</param>
		/// <remarks>Table from chapter 8.4.3 P21</remarks>
		internal static int ConvertAlphaNumChar(char inputChar)
		{
	        int value;
	        if (!s_AlphanumericTable.TryGetValue(inputChar, out value))
	        {
	            throw new ArgumentOutOfRangeException(
                    "inputChar", 
	                "Not an alphanumeric character found. Only characters from table from chapter 8.4.3 P21 are supported in alphanumeric mode.");
	        }
		    return value;
		}
		
		internal static bool Contains(char inputChar)
		{
			return s_AlphanumericTable.ContainsKey(inputChar);
		}
	}
}
