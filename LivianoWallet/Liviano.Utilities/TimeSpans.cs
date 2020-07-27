//
// TimeSpans.cs
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

namespace Liviano.Utilities
{
    /// <summary>
    /// Commonly used time spans.
    /// </summary>
    public static class TimeSpans
    {
        /// <summary>Time span of 100 milliseconds.</summary>
        public static TimeSpan Ms100 => TimeSpan.FromMilliseconds(100);

        /// <summary>Time span of 1 second.</summary>
        public static TimeSpan Second => TimeSpan.FromSeconds(1);

        /// <summary>Time span of 5 seconds.</summary>
        public static TimeSpan FiveSeconds => TimeSpan.FromSeconds(5);

        /// <summary>Time span of 10 seconds.</summary>
        public static TimeSpan TenSeconds => TimeSpan.FromSeconds(10);

        /// <summary>Time span of 1 minute.</summary>
        public static TimeSpan Minute => TimeSpan.FromMinutes(1);

        /// <summary>
        /// Special time span value used for repeat frequency values, for which it means that
        /// the event should be only run once and not repeated.
        /// </summary>
        public static TimeSpan RunOnce => TimeSpan.FromSeconds(-1);
    }
}
