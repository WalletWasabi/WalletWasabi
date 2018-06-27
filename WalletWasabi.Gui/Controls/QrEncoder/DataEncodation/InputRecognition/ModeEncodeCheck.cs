using System;
using System.Text;

namespace Gma.QrCodeNet.Encoding.DataEncodation.InputRecognition
{
	public static class ModeEncodeCheck
	{
		
		public static bool isModeEncodeValid(Mode mode, string encoding, string content)
		{
			switch(mode)
			{
				case Mode.Numeric:
					return NumericCheck(content);
				case Mode.Alphanumeric:
					return AlphaNumCheck(content);
				case Mode.EightBitByte:
					return EightBitByteCheck(encoding, content);
				case Mode.Kanji:
					return KanjiCheck(content);
				default:
					throw new InvalidOperationException(string.Format("System does not contain mode: {0}", mode.ToString()));
			}
		}
		
		private static bool NumericCheck(string content)
		{
			
			int tryEncodePos = TryEncodeAlphaNum(content, 0, content.Length);
			return tryEncodePos == -2 ? true : false;
		}
		
		
		
		private static bool AlphaNumCheck(string content)
		{
			
			int tryEncodePos = TryEncodeAlphaNum(content, 0, content.Length);
			return tryEncodePos == -1 ? true : false;
		}
		
		/// <summary>
		/// Check char from startPos for string content. 
		/// </summary>
		/// <param name="content">input string content</param>
		/// <param name="startPos">start check position</param>
		/// <returns>-2 Numeric encode, -1 AlphaNum encode, Index of failed check pos</returns>
		internal static int TryEncodeAlphaNum(string content, int startPos, int contentLength)
		{
			if(string.IsNullOrEmpty(content))
				throw new IndexOutOfRangeException("Input content should not be Null or empty");
			
			//True numeric check, False alphaNum check
			bool checkOption = true;
			
			int num = new int();
			
			for(int index = startPos; index < contentLength; index++)
			{
				if(checkOption)
				{
					num = content[index] - '0';
					if(num < 0 || num > 9)
						checkOption = false;
				}
				if(!checkOption)
				{
					if(!AlphanumericTable.Contains(content[index]))
					{
						return index;
					}
				}
				
			}
			return checkOption ? -2 : -1;
		}
		
		/// <summary>
		/// Encoding.GetEncoding.GetBytes will transform char to 0x3F if that char not belong to current encoding table. 
		/// 0x3F is '?'
		/// </summary>
		private const int QUESTION_MARK_CHAR = 0x3F;
		
		private static bool EightBitByteCheck(string encodingName, string content)
		{
			int tryEncodePos = TryEncodeEightBitByte(content, encodingName, 0, content.Length);
			return tryEncodePos == -1 ? true : false;
		}
		
		/// <summary>
		/// Use given encoding to check input string from starting position. If encoding table is suitable solution.
		/// it will return -1. Else it will return failed encoding position. 
		/// </summary>
		/// <param name="content">input string</param>
		/// <param name="encodingName">encoding name. Check ECI table</param>
		/// <param name="startPos">starting position</param>
		/// <returns>-1 if from starting position to end encoding success. Else return fail position</returns>
		internal static int TryEncodeEightBitByte(string content, string encodingName, int startPos, int contentLength)
		{
			if(string.IsNullOrEmpty(content))
				throw new IndexOutOfRangeException("Input content should not be Null or empty");
			
			System.Text.Encoding encoding;
			try
			{
				encoding = System.Text.Encoding.GetEncoding(encodingName);
			} catch(ArgumentException)
			{
				return startPos;
			}
			
			char[] currentChar = new char[1];
			byte[] bytes;
			
			
			for(int index = startPos; index < contentLength; index++)
			{
				currentChar[0] = content[index];
				bytes = encoding.GetBytes(currentChar);
				int length = bytes.Length;
				if(currentChar[0] != '?' && length == 1 && (int)bytes[0] == QUESTION_MARK_CHAR)
					return index;
				else if(length > 1)
					return index;
			}
			
			for(int index = 0; index < startPos; index++)
			{
				currentChar[0] = content[index];
				bytes = encoding.GetBytes(currentChar);
				int length = bytes.Length;
				if(currentChar[0] != '?' && length == 1 && (int)bytes[0] == QUESTION_MARK_CHAR)
					return index;
				else if(length > 1)
					return index;
			}
			
			return -1;
		}
		
		private static bool KanjiCheck(string content)
		{
			int tryEncodePos = TryEncodeKanji(content, content.Length);
			return tryEncodePos == -1 ? true : false;
		}
		
		/// <summary>
		/// Check input string content. Whether it can apply Kanji encode or not. 
		/// </summary>
		/// <param name="content">String input content</param>
		/// <returns>-1 if it can apply Kanji encode, -2 should use utf8 encode, 0 check for other encode.</returns>
		internal static int TryEncodeKanji(string content, int contentLength)
		{
			if(string.IsNullOrEmpty(content))
				throw new IndexOutOfRangeException("Input content should not be Null or empty");
			System.Text.Encoding encoding;
			try
			{
				encoding = System.Text.Encoding.GetEncoding("Shift_JIS");
			} catch(ArgumentException)
			{
				return 0;
			}
			
			char[] currentChar = new char[1];
			byte[] bytes;
			
			for(int index = 0; index < contentLength; index++)
			{
				currentChar[0] = content[index];
				bytes = encoding.GetBytes(currentChar);
				int length = bytes.Length;
				byte mostSignificantByte = bytes[0];
				
				if(length != 2)
					return index == 0 ? 0 : -2;
				else if((mostSignificantByte < 0x81 || mostSignificantByte > 0x9F) && (mostSignificantByte < 0xE0 || mostSignificantByte > 0xEB))
				{
					return index == 0 ? 0 : -2;
				}
				
			}
			
			return -1;
		}
		
		
	}
}
