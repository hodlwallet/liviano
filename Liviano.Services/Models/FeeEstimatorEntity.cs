//
// FeeEstimatorEntity.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2022 HODL Wallet
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
namespace Liviano.Services.Models
{
    public class FeeEstimatorEntity
    {
        // Example response:
        //
        // {
        // 	"fastest_blocks": 2,
        // 	"fastest_btc_per_kilobyte": 0.00022187,
        // 	"fastest_sat_per_kilobyte": 22187,
        // 	"fastest_time": 60,
        // 	"fastest_time_text": "10 - 45 minutes",
        // 	"normal_blocks": 10,
        // 	"normal_btc_per_kilobyte": 0.00010699,
        // 	"normal_sat_per_kilobyte": 10699,
        // 	"normal_time": 180,
        // 	"normal_time_text": "1 - 2 hours",
        // 	"slow_blocks": 45,
        // 	"slow_btc_per_kilobyte": 1e-05,
        // 	"slow_sat_per_kilobyte": 1000,
        // 	"slow_time": 750,
        // 	"slow_time_text": "3 - 24 hours"
        // }

        public int fastest_blocks { get; set; }
        public double fastest_btc_per_kilobyte { get; set; }
        public int fastest_sat_per_kilobyte { get; set; }
        public int fastest_time { get; set; }
        public string fastest_time_text { get; set; }

        public int normal_blocks { get; set; }
        public double normal_btc_per_kilobyte { get; set; }
        public int normal_sat_per_kilobyte { get; set; }
        public int normal_time { get; set; }
        public string normal_time_text { get; set; }

        public int slow_blocks { get; set; }
        public double slow_btc_per_kilobyte { get; set; }
        public int slow_sat_per_kilobyte { get; set; }
        public int slow_time { get; set; }
        public string slow_time_text { get; set; }
    }
}
