/*
 * Copyright 2016 ZXing authors
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

using NUnit.Framework;

using ZXing.Common.Test;

namespace ZXing.OneD.Test
{
    public class Code93WriterTestCase
    {
        [Test]
        public void testEncode()
        {
            doTest("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789",
                "000001010111101101010001101001001101000101100101001100100101100010101011010001011001" +
                "001011000101001101001000110101010110001010011001010001101001011001000101101101101001" +
                "101100101101011001101001101100101101100110101011011001011001101001101101001110101000" +
                "101001010010001010001001010000101001010001001001001001000101010100001000100101000010" +
                "10100111010101000010101011110100000");

            doTest("\u0000\u0001\u001a\u001b\u001f $%+!,09:;@AZ[_`az{\u007f",
                "00000" + "101011110" +
                "111011010" + "110010110" + "100100110" + "110101000" + // bU aA
                "100100110" + "100111010" + "111011010" + "110101000" + // aZ bA
                "111011010" + "110010010" + "111010010" + "111001010" + // bE space $
                "110101110" + "101110110" + "111010110" + "110101000" + // % + cA
                "111010110" + "101011000" + "100010100" + "100001010" + // cL 0 9
                "111010110" + "100111010" + "111011010" + "110001010" + // cZ bF
                "111011010" + "110011010" + "110101000" + "100111010" + // bV A Z
                "111011010" + "100011010" + "111011010" + "100101100" + // bK bO
                "111011010" + "101101100" + "100110010" + "110101000" + // bW dA
                "100110010" + "100111010" + "111011010" + "100010110" + // dZ bP
                "111011010" + "110100110" + // bT
                "110100010" + "110101100" + // checksum: 12 28
                "101011110" + "100000");
        }

        [Test]
        public void testConvertToExtended()
        {
            // non-extended chars are not changed.
            var src = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%";
            var dst = Code93Writer.convertToExtended(src);
            Assert.That(dst, Is.EqualTo(src));
        }

        private static void doTest(String input, String expected)
        {
            var result = new Code93Writer() { DefaultMargin = 5 }.encode(input, BarcodeFormat.CODE_93, 0, 0);
            Assert.AreEqual(expected, BitMatrixTestCase.matrixToString(result));
        }
    }
}