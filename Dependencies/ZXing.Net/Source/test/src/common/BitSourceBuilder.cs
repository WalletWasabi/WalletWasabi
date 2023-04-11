/*
 * Copyright 2008 ZXing authors
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

using System.IO;

namespace ZXing.Common.Test
{
   /// <summary>
   /// Class that lets one easily build an array of bytes by appending bits at a time.
   ///
   /// <author>Sean Owen</author>
   /// </summary>
   public sealed class BitSourceBuilder
   {
      private MemoryStream output;
      private int nextByte;
      private int bitsLeftInNextByte;

      public BitSourceBuilder()
      {
         output = new MemoryStream();
         nextByte = 0;
         bitsLeftInNextByte = 8;
      }

      public void write(int value, int numBits)
      {
         if (numBits <= bitsLeftInNextByte)
         {
            nextByte <<= numBits;
            nextByte |= value;
            bitsLeftInNextByte -= numBits;
            if (bitsLeftInNextByte == 0)
            {
               output.WriteByte((byte) nextByte);
               nextByte = 0;
               bitsLeftInNextByte = 8;
            }
         }
         else
         {
            int bitsToWriteNow = bitsLeftInNextByte;
            int numRestOfBits = numBits - bitsToWriteNow;
            int mask = 0xFF >> (8 - bitsToWriteNow);
            int valueToWriteNow = ((int)((uint)value >> numRestOfBits)) & mask;
            write(valueToWriteNow, bitsToWriteNow);
            write(value, numRestOfBits);
         }
      }

      public byte[] toByteArray()
      {
         if (bitsLeftInNextByte < 8)
         {
            write(0, bitsLeftInNextByte);
         }
         return output.ToArray();
      }
   }
}