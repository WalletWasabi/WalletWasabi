/*
 * Copyright 2011 ZXing authors
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

using ZXing.Common.Test;

namespace ZXing.OneD.Test
{
   /// <summary>
   /// A set of blurry images taken with a fixed-focus device.
   /// <author>dswitkin@google.com (Daniel Switkin)</author>
   /// </summary>
   public sealed class EAN13BlackBox5BlurryTestCase : AbstractBlackBoxTestCase
   {
      public EAN13BlackBox5BlurryTestCase()
         : base("test/data/blackbox/ean13-5", new MultiFormatReader(), BarcodeFormat.EAN_13)
      {
         addTest(0, 0, 0.0f);
         addTest(0, 0, 180.0f);
      }
   }
}