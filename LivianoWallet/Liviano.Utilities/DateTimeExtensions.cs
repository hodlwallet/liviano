//
// DateTimeExtensions.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 
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

namespace Liviano.Utilities.Extenssions
{
    /// <summary>
    /// Provides a set of extension methods for the <see cref="DateTime"/> class.
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Converts a given DateTime into a Unix timestamp.
        /// </summary>
        /// <param name="value">Any DateTime</param>
        /// <returns>The given DateTime in Unix timestamp format</returns>
        /// <remarks>This represents the number of seconds that have elapsed since 1970-01-01T00:00:00Z.</remarks>
        public static int ToUnixTimestamp(this DateTime value)
        {
            return (int)((DateTimeOffset)value).ToUnixTimeSeconds();
        }
    }
}
