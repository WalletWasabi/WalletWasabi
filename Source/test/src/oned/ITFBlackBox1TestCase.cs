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

using ZXing.Common.Test;

namespace ZXing.OneD.Test
{
   /// <summary>
   /// <author>kevin.osullivan@sita.aero</author>
   /// </summary>
   public sealed class ITFBlackBox1TestCase : AbstractBlackBoxTestCase
   {
      public ITFBlackBox1TestCase()
         : base("test/data/blackbox/itf-1", new MultiFormatReader(), BarcodeFormat.ITF)
      {
         addTest(14, 14, 0.0f);
         addTest(14, 14, 180.0f);
      }
   }
}
