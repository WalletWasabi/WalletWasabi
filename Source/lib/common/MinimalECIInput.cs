﻿/*
 * Copyright 2021 ZXing authors
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

namespace ZXing.Common
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Class that converts a character string into a sequence of ECIs and bytes
    /// The implementation uses the Dijkstra algorithm to produce minimal encodings
    /// @author Alex Geller
    /// </summary>
    public class MinimalECIInput : ECIInput
    {
        private static int COST_PER_ECI = 3; // approximated (latch + 2 codewords)
        private int[] bytes;
        private int fnc1;

        /// <summary>
        /// Constructs a minimal input
        /// </summary>
        /// <param name="stringToEncode">the character string to encode</param>
        /// <param name="priorityCharset">The preferred {@link Charset}. When the value of the argument is null, the algorithm
        /// chooses charsets that leads to a minimal representation. Otherwise the algorithm will use the priority
        /// charset to encode any character in the input that can be encoded by it if the charset is among the
        /// supported charsets.</param>
        /// <param name="fnc1">denotes the character in the input that represents the FNC1 character or -1 if this is not GS1
        /// input.</param>
        public MinimalECIInput(String stringToEncode, Encoding priorityCharset, int fnc1)
        {
            this.fnc1 = fnc1;
            var encoderSet = new ECIEncoderSet(stringToEncode, priorityCharset, fnc1);
            if (encoderSet.Length == 1)
            { //optimization for the case when all can be encoded without ECI in ISO-8859-1
                bytes = new int[stringToEncode.Length];
                for (int i = 0; i < bytes.Length; i++)
                {
                    char c = stringToEncode[i];
                    bytes[i] = c == fnc1 ? 1000 : (int)c;
                }
            }
            else
            {
                bytes = encodeMinimally(stringToEncode, encoderSet, fnc1);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int getFNC1Character()
        {
            return fnc1;
        }

        /// <summary>
        /// Returns the length of this input.  The length is the number
        /// of {@code byte}s, FNC1 characters or ECIs in the sequence.
        /// </summary>
        public int Length
        {
            get
            {
                return bytes.Length;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public bool haveNCharacters(int index, int n)
        {
            if (index + n - 1 >= bytes.Length)
            {
                return false;
            }
            for (int i = 0; i < n; i++)
            {
                if (isECI(index + i))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns the {@code byte} value at the specified index.  An index ranges from zero
        /// to {@code length() - 1}.  The first {@code byte} value of the sequence is at
        /// index zero, the next at index one, and so on, as for array
        /// indexing.
        /// </summary>
        /// <param name="index">the index of the {@code byte} value to be returned</param>
        /// <returns>the specified {@code byte} value as character or the FNC1 character</returns>
        /// <exception cref="IndexOutOfRangeException">if the {@code index} argument is negative or not less than
        /// {@code length()}</exception>
        /// <exception cref="ArgumentException">if the value at the {@code index} argument is an ECI (@see #isECI)</exception>
        public char charAt(int index)
        {
            if (index < 0 || index >= Length)
            {
                throw new IndexOutOfRangeException("" + index);
            }
            if (isECI(index))
            {
                throw new ArgumentException("value at " + index + " is not a character but an ECI");
            }
            return isFNC1(index) ? (char)fnc1 : (char)bytes[index];
        }

        /// <summary>
        /// Returns a {@code CharSequence} that is a subsequence of this sequence.
        /// The subsequence starts with the {@code char} value at the specified index and
        /// ends with the {@code char} value at index {@code end - 1}.  The length
        /// (in {@code char}s) of the
        /// returned sequence is {@code end - start}, so if {@code start == end}
        /// then an empty sequence is returned.
        /// </summary>
        /// <param name="start">the start index, inclusive</param>
        /// <param name="end">the end index, exclusive</param>
        /// <returns>the specified subsequence</returns>
        /// <exception cref="IndexOutOfRangeException">if {@code start} or {@code end} are negative,
        /// if {@code end} is greater than {@code length()},
        /// or if {@code start} is greater than {@code end}</exception>
        /// <exception cref="ArgumentException">if a value in the range {@code start}-{@code end} is an ECI (@see #isECI)</exception>
        public String subSequence(int start, int end)
        {
            if (start < 0 || start > end || end > Length)
            {
                throw new IndexOutOfRangeException("" + start);
            }
            var result = new StringBuilder();
            for (int i = start; i < end; i++)
            {
                if (isECI(i))
                {
                    throw new ArgumentException("value at " + i + " is not a character but an ECI");
                }
                result.Append(charAt(i));
            }
            return result.ToString();
        }

        /// <summary>
        /// Determines if a value is an ECI
        /// </summary>
        /// <param name="index">the index of the value</param>
        /// <returns>true if the value at position {@code index} is an ECI</returns>
        /// <exception cref="IndexOutOfRangeException">if the {@code index} argument is negative or not less than
        /// {@code length()}</exception>
        public bool isECI(int index)
        {
            if (index < 0 || index >= Length)
            {
                throw new IndexOutOfRangeException("" + index);
            }
            return bytes[index] > 255 && bytes[index] <= 999;
        }

        /// <summary>
        /// Determines if a value is the FNC1 character
        /// </summary>
        /// <param name="index">the index of the value</param>
        /// <returns>true if the value at position {@code index} is the FNC1 character</returns>
        /// <exception cref="IndexOutOfRangeException">if the {@code index} argument is negative or not less than
        /// {@code length()}</exception>
        public bool isFNC1(int index)
        {
            if (index < 0 || index >= Length)
            {
                throw new IndexOutOfRangeException("" + index);
            }
            return bytes[index] == 1000;
        }

        /// <summary>
        /// Returns the {@code int} ECI value at the specified index.  An index ranges from zero
        /// to {@code length() - 1}.  The first {@code byte} value of the sequence is at
        /// index zero, the next at index one, and so on, as for array
        /// indexing.
        /// </summary>
        /// <param name="index">the index of the {@code int} value to be returned</param>
        /// <returns>the specified {@code int} ECI value.
        /// The ECI specified the encoding of all bytes with a higher index until the
        /// next ECI or until the end of the input if no other ECI follows.</returns>
        /// <exception cref="IndexOutOfRangeException">if the {@code index} argument is negative or not less than
        /// {@code length()}</exception>
        /// <exception cref="ArgumentException">if the value at the {@code index} argument is not an ECI (@see #isECI)</exception>
        public int getECIValue(int index)
        {
            if (index < 0 || index >= Length)
            {
                throw new IndexOutOfRangeException("" + index);
            }
            if (!isECI(index))
            {
                throw new ArgumentException("value at " + index + " is not an ECI but a character");
            }
            return bytes[index] - 256;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override String ToString()
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < Length; i++)
            {
                if (i > 0)
                {
                    result.Append(", ");
                }
                if (isECI(i))
                {
                    result.Append("ECI(");
                    result.Append(getECIValue(i));
                    result.Append(')');
                }
                else if (charAt(i) < 128)
                {
                    result.Append('\'');
                    result.Append(charAt(i));
                    result.Append('\'');
                }
                else
                {
                    result.Append((int)charAt(i));
                }
            }
            return result.ToString();
        }

        static void addEdge(InputEdge[][] edges, int to, InputEdge edge)
        {
            if (edges[to][edge.encoderIndex] == null ||
                edges[to][edge.encoderIndex].cachedTotalSize > edge.cachedTotalSize)
            {
                edges[to][edge.encoderIndex] = edge;
            }
        }

        static void addEdges(String stringToEncode,
                             ECIEncoderSet encoderSet,
                             InputEdge[][] edges,
                             int from,
                             InputEdge previous,
                             int fnc1)
        {

            char ch = stringToEncode[from];

            int start = 0;
            int end = encoderSet.Length;
            if (encoderSet.getPriorityEncoderIndex() >= 0 && (ch == fnc1 || encoderSet.canEncode(ch,
                encoderSet.getPriorityEncoderIndex())))
            {
                start = encoderSet.getPriorityEncoderIndex();
                end = start + 1;
            }

            for (int i = start; i < end; i++)
            {
                if (ch == fnc1 || encoderSet.canEncode(ch, i))
                {
                    addEdge(edges, from + 1, new InputEdge(ch, encoderSet, i, previous, fnc1));
                }
            }
        }

        static int[] encodeMinimally(String stringToEncode, ECIEncoderSet encoderSet, int fnc1)
        {
            int inputLength = stringToEncode.Length;

            // Array that represents vertices. There is a vertex for every character and encoding.
            var edges = new InputEdge[inputLength + 1][];
            for (var x = 0; x < edges.Length; x++)
            {
                edges[x] = new InputEdge[encoderSet.Length];
            }

            addEdges(stringToEncode, encoderSet, edges, 0, null, fnc1);

            for (int i = 1; i <= inputLength; i++)
            {
                for (int j = 0; j < encoderSet.Length; j++)
                {
                    if (edges[i][j] != null && i < inputLength)
                    {
                        addEdges(stringToEncode, encoderSet, edges, i, edges[i][j], fnc1);
                    }
                }
                //optimize memory by removing edges that have been passed.
                for (int j = 0; j < encoderSet.Length; j++)
                {
                    edges[i - 1][j] = null;
                }
            }
            int minimalJ = -1;
            int minimalSize = Int32.MaxValue;
            for (int j = 0; j < encoderSet.Length; j++)
            {
                if (edges[inputLength][j] != null)
                {
                    InputEdge edge = edges[inputLength][j];
                    if (edge.cachedTotalSize < minimalSize)
                    {
                        minimalSize = edge.cachedTotalSize;
                        minimalJ = j;
                    }
                }
            }
            if (minimalJ < 0)
            {
                throw new InvalidOperationException("Failed to encode \"" + stringToEncode + "\"");
            }
            var intsAL = new List<int>();
            InputEdge current = edges[inputLength][minimalJ];
            while (current != null)
            {
                if (current.isFNC1)
                {
                    intsAL.Insert(0, 1000);
                }
                else
                {
                    byte[] bytes = encoderSet.encode(current.c, current.encoderIndex);
                    for (int i = bytes.Length - 1; i >= 0; i--)
                    {
                        intsAL.Insert(0, (bytes[i] & 0xFF));
                    }
                }
                int previousEncoderIndex = current.previous == null ? 0 : current.previous.encoderIndex;
                if (previousEncoderIndex != current.encoderIndex)
                {
                    intsAL.Insert(0, 256 + encoderSet.getECIValue(current.encoderIndex));
                }
                current = current.previous;
            }
            int[] ints = new int[intsAL.Count];
            for (int i = 0; i < ints.Length; i++)
            {
                ints[i] = intsAL[i];
            }
            return ints;
        }

        internal class InputEdge
        {
            internal char c;
            private bool cIsFnc1;
            internal int encoderIndex; //the encoding of this edge
            internal InputEdge previous;
            internal int cachedTotalSize;

            internal InputEdge(char c, ECIEncoderSet encoderSet, int encoderIndex, InputEdge previous, int fnc1)
            {
                this.c = c;
                this.cIsFnc1 = (int)c == fnc1;
                this.encoderIndex = encoderIndex;
                this.previous = previous;

                int size = cIsFnc1 ? 1 : encoderSet.encode(c, encoderIndex).Length;
                int previousEncoderIndex = previous == null ? 0 : previous.encoderIndex;
                if (previousEncoderIndex != encoderIndex)
                {
                    size += COST_PER_ECI;
                }
                if (previous != null)
                {
                    size += previous.cachedTotalSize;
                }
                this.cachedTotalSize = size;
            }

            internal bool isFNC1
            {
                get
                {
                    return cIsFnc1;
                }
            }
        }
    }
}
