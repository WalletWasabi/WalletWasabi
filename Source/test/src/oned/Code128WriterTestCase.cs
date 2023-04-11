/*
 * Copyright 2014 ZXing authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;

using NUnit.Framework;
using ZXing.Common;
using ZXing.Common.Test;

namespace ZXing.OneD.Test
{
    public class Code128WriterTestCase
    {
        private const String FNC1 = "11110101110";
        private const String FNC2 = "11110101000";
        private const String FNC3 = "10111100010";
        private const String FNC4A = "11101011110";
        private const String FNC4B = "10111101110";
        private const String START_CODE_A = "11010000100";
        private const String START_CODE_B = "11010010000";
        private const String START_CODE_C = "11010011100";
        private const String SWITCH_CODE_A = "11101011110";
        private const String SWITCH_CODE_B = "10111101110";
        private const String QUIET_SPACE = "00000";
        private const String STOP = "1100011101011";
        private const String LF = "10000110010";

        private Writer writer;
        private Code128Reader reader;

        [SetUp]
        public void setUp()
        {
            writer = new Code128Writer() { DefaultMargin = 5 };
            reader = new Code128Reader();
        }

        [Test]
        public void testEncodeWithFunc3()
        {
            const string toEncode = "\u00f3" + "123";
            //                                                       "1"            "2"             "3"          check digit 51
            var expected = QUIET_SPACE + START_CODE_B + FNC3 + "10011100110" + "11001110010" + "11001011100" +
                           "11101000110" + STOP + QUIET_SPACE;

            var result = encode(toEncode, false, "123");

            var actual = BitMatrixTestCase.matrixToString(result);

            Assert.AreEqual(expected, actual);

            int width = result.Width;
            result = encode(toEncode, true, "123");

            Assert.AreEqual(width, result.Width);
        }

        [Test]
        public void testEncodeWithFunc2()
        {
            const string toEncode = "\u00f2" + "123";
            //                                                       "1"            "2"             "3"          check digit 56
            var expected = QUIET_SPACE + START_CODE_B + FNC2 + "10011100110" + "11001110010" + "11001011100" +
                           "11100010110" + STOP + QUIET_SPACE;

            var result = encode(toEncode, false, "123");

            var actual = BitMatrixTestCase.matrixToString(result);

            Assert.AreEqual(expected, actual);

            int width = result.Width;
            result = encode(toEncode, true, "123");

            Assert.AreEqual(width, result.Width);
        }

        [Test]
        public void testEncodeWithFunc1()
        {
            const string toEncode = "\u00f1" + "123";
            //                                                       "12"                           "3"          check digit 92
            var expected = QUIET_SPACE + START_CODE_C + FNC1 + "10110011100" + SWITCH_CODE_B + "11001011100" +
                           "10101111000" + STOP + QUIET_SPACE;

            var result = encode(toEncode, false, "123");

            var actual = BitMatrixTestCase.matrixToString(result);

            Assert.AreEqual(expected, actual);

            int width = result.Width;
            result = encode(toEncode, true, "123");

            Assert.AreEqual(width, result.Width);
        }

        [Test]
        public void testRoundtrip()
        {
            var toEncode = "\u00f1" + "10958" + "\u00f1" + "17160526";
            var expected = "1095817160526";

            var encResult = encode(toEncode, false, expected);
            int width = encResult.Width;
            encResult = encode(toEncode, true, expected);
            //Compact encoding has one latch less and encodes as STARTA,FNC1,1,CODEC,09,58,FNC1,17,16,05,26
            Assert.AreEqual(width, encResult.Width + 11);
        }

        [Test]
        public void testLongCompact()
        {
            //test longest possible input
            var toEncode = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            encode(toEncode, true, toEncode);
        }

        [Test]
        public void testShift()
        {
            //compare fast to compact
            var toEncode = "a\na\na\na\na\na\na\na\na\na\na\na\na\na\na\na\na\na\na\na\na\na\na\na\n";
            var result = encode(toEncode, false, toEncode);

            int width = result.Width;
            result = encode(toEncode, true, toEncode);

            //big difference since the fast algoritm doesn't make use of SHIFT
            Assert.AreEqual(width, result.Width + 253);
        }

        [Test]
        public void testDigitMixCompaction()
        {
            //compare fast to compact
            var toEncode = "A1A12A123A1234A12345AA1AA12AA123AA1234AA1235";
            var result = encode(toEncode, false, toEncode);

            int width = result.Width;
            result = encode(toEncode, true, toEncode);

            //very good, no difference
            Assert.AreEqual(width, result.Width);
        }

        [Test]
        public void testCompaction1()
        {
            //compare fast to compact
            var toEncode = "AAAAAAAAAAA12AAAAAAAAA";
            var result = encode(toEncode, false, toEncode);

            int width = result.Width;
            result = encode(toEncode, true, toEncode);

            //very good, no difference
            Assert.AreEqual(width, result.Width);
        }

        [Test]
        public void testCompaction2()
        {
            //compare fast to compact
            var toEncode = "AAAAAAAAAAA1212aaaaaaaaa";
            var result = encode(toEncode, false, toEncode);

            int width = result.Width;
            result = encode(toEncode, true, toEncode);

            //very good, no difference
            Assert.AreEqual(width, result.Width);
        }


        [Test]
        public void testEncodeWithFunc4()
        {
            var toEncode = "\u00f4" + "123";
            //                                                       "1"            "2"             "3"          check digit 59
            var expected = QUIET_SPACE + START_CODE_B + FNC4B + "10011100110" + "11001110010" + "11001011100" +
                           "11100011010" + STOP + QUIET_SPACE;

            var result = encode(toEncode, false, null);

            var actual = BitMatrixTestCase.matrixToString(result);

            Assert.AreEqual(expected, actual);

            int width = result.Width;
            result = encode(toEncode, true, null);

            Assert.AreEqual(width, result.Width);
        }

        [Test]
        public void testEncodeWithFncsAndNumberInCodesetA()
        {
            var toEncode = "\n" + "\u00f1" + "\u00f4" + "1" + "\n";

            var expected = QUIET_SPACE + START_CODE_A + LF + FNC1 + FNC4A + "10011100110" + LF + "10101111000" + STOP + QUIET_SPACE;

            var result = encode(toEncode, false, null);

            var actual = BitMatrixTestCase.matrixToString(result);

            Assert.That(actual, Is.EqualTo(expected));

            int width = result.Width;
            result = encode(toEncode, true, null);

            Assert.AreEqual(width, result.Width);
        }

        [Test]
        public void Should_Encode_And_Decode_Roundtrip()
        {
            var contents = String.Empty;

            for (var i = 0; i < 128; i++)
            {
                contents += (char)i;
                if ((i + 1) % 32 == 0)
                {
                    Should_Encode(contents);
                    contents = String.Empty;
                }
            }
        }

        [TestCase("\0ABab\u0010", TestName = "Start with A, switch to B and back to A")]
        [TestCase("ab\0ab", TestName = "Start with B, switch to A and back to B")]
        public void Should_Encode(string contents)
        {
            var sut = new Code128Writer();
            var sutDecode = new Code128Reader();

            var result = sut.encode(contents, BarcodeFormat.CODE_128, 0, 0);
            var resultString = BitMatrixTestCase.matrixToString(result);
            Console.WriteLine(contents);
            Console.WriteLine(resultString);
            Console.WriteLine("");
            var matrix = BitMatrix.parse(resultString, "1", "0");
            var row = new BitArray(matrix.Width);
            matrix.getRow(0, row);
            var decodingResult = sutDecode.decodeRow(0, row, null);
            Assert.That(decodingResult, Is.Not.Null);
            Assert.That(decodingResult.Text, Is.EqualTo(contents));
        }

        [Test]
        public void testEncodeSwitchBetweenCodesetsAAndB()
        {
            // start with A switch to B and back to A
            //                                                      "\0"            "A"             "B"             Switch to B     "a"             "b"             Switch to A     "\u0010"        check digit
            testEncode("\0ABab\u0010",
                QUIET_SPACE + START_CODE_A + "10100001100" + "10100011000" + "10001011000" + SWITCH_CODE_B + "10010110000" + "10010000110" + SWITCH_CODE_A + "10100111100" + "11001110100" + STOP + QUIET_SPACE);

            // start with B switch to A and back to B
            // the compact encoder encodes this shorter as STARTB,a,b,SHIFT,NUL,a,b
            //                                                "a"             "b"             Switch to A     "\0             "Switch to B"   "a"             "b"             check digit
            testEncode("ab\0ab",
                QUIET_SPACE + START_CODE_B + "10010110000" + "10010000110" + SWITCH_CODE_A + "10100001100" + SWITCH_CODE_B + "10010110000" + "10010000110" + "11010001110" + STOP + QUIET_SPACE);
        }

        private void testEncode(String toEncode, String expected)
        {
            var result = encode(toEncode, false, toEncode);

            var actual = BitMatrixTestCase.matrixToString(result);
            Assert.AreEqual(expected, actual, toEncode);

            var row = result.getRow(0, null);
            var rtResult = reader.decodeRow(0, row, null);
            var actualRoundtripResultText = rtResult.Text;
            Assert.AreEqual(toEncode, actualRoundtripResultText);

            int width = result.Width;
            result = encode(toEncode, true, toEncode);
            Assert.True(result.Width <= width);
        }


        [Test]
        public void testEncodeWithForcedCodeSetFailureCodeSetABadCharacter()
        {
            // Lower case characters should not be accepted when the code set is forced to A.
            String toEncode = "ASDFx0123";

            var options = new Code128EncodingOptions();
            options.ForceCodeset = Code128EncodingOptions.Codesets.A;
            Assert.Throws<ArgumentException>(() => writer.encode(toEncode, BarcodeFormat.CODE_128, 0, 0, options.Hints));
        }

        [Test]
        public void testEncodeWithForcedCodeSetFailureCodeSetBBadCharacter()
        {
            String toEncode = "ASdf\00123"; // \0 (ascii value 0)
                                            // Characters with ASCII value below 32 should not be accepted when the code set is forced to B.

            var options = new Code128EncodingOptions();
            options.ForceCodeset = Code128EncodingOptions.Codesets.B;
            Assert.Throws<ArgumentException>(() => writer.encode(toEncode, BarcodeFormat.CODE_128, 0, 0, options.Hints));
        }

        [Test]
        public void testEncodeWithForcedCodeSetFailureCodeSetCBadCharactersNonNum()
        {
            String toEncode = "123a5678";
            // Non-digit characters should not be accepted when the code set is forced to C.

            var options = new Code128EncodingOptions();
            options.ForceCodeset = Code128EncodingOptions.Codesets.C;
            Assert.Throws<ArgumentException>(() => writer.encode(toEncode, BarcodeFormat.CODE_128, 0, 0, options.Hints));
        }

        [Test]
        public void testEncodeWithForcedCodeSetFailureCodeSetCBadCharactersFncCode()
        {
            String toEncode = "123\u00f2a678";
            // Function codes other than 1 should not be accepted when the code set is forced to C.

            var options = new Code128EncodingOptions();
            options.ForceCodeset = Code128EncodingOptions.Codesets.C;
            Assert.Throws<ArgumentException>(() => writer.encode(toEncode, BarcodeFormat.CODE_128, 0, 0, options.Hints));
        }

        [Test]
        public void testEncodeWithForcedCodeSetFailureCodeSetCWrongAmountOfDigits()
        {
            String toEncode = "123456789";
            // An uneven amount of digits should not be accepted when the code set is forced to C.

            var options = new Code128EncodingOptions();
            options.ForceCodeset = Code128EncodingOptions.Codesets.C;
            Assert.Throws<ArgumentException>(() => writer.encode(toEncode, BarcodeFormat.CODE_128, 0, 0, options.Hints));
        }

        [Test]
        public void testEncodeWithForcedCodeSetFailureCodeSetA()
        {
            String toEncode = "AB123";
            //                          would default to B             "A"             "B"             "1"             "2"             "3"  check digit 10
            String expected = QUIET_SPACE + START_CODE_A + "10100011000" + "10001011000" + "10011100110" + "11001110010" + "11001011100" + "11001000100" + STOP + QUIET_SPACE;

            var options = new Code128EncodingOptions();
            options.ForceCodeset = Code128EncodingOptions.Codesets.A;
            BitMatrix result = writer.encode(toEncode, BarcodeFormat.CODE_128, 0, 0, options.Hints);

            String actual = BitMatrixTestCase.matrixToString(result);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void testEncodeWithForcedCodeSetFailureCodeSetB()
        {
            String toEncode = "1234";
            //                          would default to C           "1"             "2"             "3"             "4"  check digit 88
            String expected = QUIET_SPACE + START_CODE_B + "10011100110" + "11001110010" + "11001011100" + "11001001110" + "11110010010" + STOP + QUIET_SPACE;

            var options = new Code128EncodingOptions();
            options.ForceCodeset = Code128EncodingOptions.Codesets.B;
            BitMatrix result = writer.encode(toEncode, BarcodeFormat.CODE_128, 0, 0, options.Hints);

            String actual = BitMatrixTestCase.matrixToString(result);
            Assert.AreEqual(expected, actual);
        }

        private BitMatrix encode(String toEncode, bool compact, String expectedLoopback)
        {
            var hints = new Dictionary<EncodeHintType, object>();
            if (compact)
            {
                hints[EncodeHintType.CODE128_COMPACT] = true;
            }
            var encResult = writer.encode(toEncode, BarcodeFormat.CODE_128, 0, 0, hints);
            if (expectedLoopback != null)
            {
                var row = encResult.getRow(0, null);
                var rtResult = reader.decodeRow(0, row, null);
                var actual = rtResult.Text;
                Assert.AreEqual(expectedLoopback, actual);
            }
            if (compact)
            {
                //check that what is encoded compactly yields the same on loopback as what was encoded fast.
                var row = encResult.getRow(0, null);
                var rtResult = reader.decodeRow(0, row, null);
                var actual = rtResult.Text;
                var encResultFast = writer.encode(toEncode, BarcodeFormat.CODE_128, 0, 0);
                row = encResultFast.getRow(0, null);
                rtResult = reader.decodeRow(0, row, null);
                Assert.AreEqual(rtResult.Text, actual);
            }
            return encResult;
        }
    }
}