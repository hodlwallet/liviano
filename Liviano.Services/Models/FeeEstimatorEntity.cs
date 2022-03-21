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
using Newtonsoft.Json;

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

        [JsonProperty("fastest_blocks")]
        public int FastestBlocks { get; set; }

        [JsonProperty("fastest_btc_per_kilobyte")]
        public double FastestBtcPerKilobyte { get; set; }

        [JsonProperty("fastest_sat_per_kilobyte")]
        public int FastestSatPerKilobyte { get; set; }

        [JsonProperty("fastest_time")]
        public int FastestTime { get; set; }

        [JsonProperty("fastest_time_text")]
        public string FastestTimeText { get; set; }

        [JsonProperty("normal_blocks")]
        public int NormalBlocks { get; set; }

        [JsonProperty("normal_btc_per_kilobyte")]
        public double NormalBtcPerKilobyte { get; set; }

        [JsonProperty("normal_sat_per_kilobyte")]
        public int NormalSatPerKilobyte { get; set; }

        [JsonProperty("normal_time")]
        public int NormalTime { get; set; }

        [JsonProperty("normal_time_text")]
        public string NormalTimeText { get; set; }

        [JsonProperty("slow_blocks")]
        public int SlowBlocks { get; set; }

        [JsonProperty("slow_btc_per_kilobyte")]
        public double SlowBtcPerKilobyte { get; set; }

        [JsonProperty("slow_sat_per_kilobyte")]
        public int SlowSatPerKilobyte { get; set; }

        [JsonProperty("slow_time")]
        public int SlowTime { get; set; }

        [JsonProperty("slow_time_text")]
        public string SlowTimeText { get; set; }
    }
}
