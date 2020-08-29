//
// Guard.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 HODL Wallet
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;

namespace Liviano.Utilities
{
    public class Guard
    {
        /// <summary>
        /// Asserts that a condition is true.
        /// </summary>
        /// <param name="condition">The condition to assert.</param>
        public static void Assert(bool condition)
        {
            if (!condition)
                throw new Exception("Assertion failed");
        }

        /// <summary>
        /// Checks an object is not null.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="value">The object.</param>
        /// <param name="parameterName">The name of the object.</param>
        /// <returns>The object if it is not null.</returns>
        /// <exception cref="ArgumentNullException">An exception if the object passed is null.</exception>
        public static T NotNull<T>(T value, string parameterName)
        {
            // the parameterName should never be null or empty
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentNullException(parameterName);
            }

            // throw if the value is null
            if (ReferenceEquals(value, null))
            {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        /// <summary>
        /// Checks a <see cref="string"/> is not null or empty.
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <param name="parameterName">The name of the string.</param>
        /// <returns>The string if it is not null or empty.</returns>
        public static string NotEmpty(string value, string parameterName)
        {
            NotNull(value, parameterName);

            if (value.Trim().Length == 0)
            {
                throw new ArgumentException($"The string parameter {parameterName} cannot be empty.");
            }

            return value;
        }
    }
}
