using System;
using Xunit;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.IO;

using Liviano.Utilities.JsonConverters;

namespace Liviano.Tests
{
    public class JsonConvertersTest
    {
        private void JsonError(Exception innerException = null)
        {
            string jsonError = "this is not valid json";
            JsonTextReader jsonTextReader = new JsonTextReader(new StringReader(jsonError));

            if (innerException != null)
            {
                throw new JsonObjectException(innerException, jsonTextReader);
            }
            else
            {
                throw new JsonObjectException("Error", jsonTextReader);
            }
        }

        [Fact]
        public void JsonObjectExceptionTest()
        {
            Assert.Throws<JsonObjectException>(() => JsonError());
            Assert.Throws<JsonObjectException>(() => JsonError(new Exception("Error")));
        }

        [Fact]
        public void NetworkConverterTest()
        {
            NetworkConverter networkConverter = new NetworkConverter();
            Network mainNetwork = Network.Main;

            string serializedResult;
            Network deserializedResult;

            Assert.True(networkConverter.CanConvert(typeof(Network)));

            serializedResult = JsonConvert.SerializeObject(mainNetwork, networkConverter);
            Assert.Equal($"\"{mainNetwork}\"", serializedResult);

            deserializedResult = JsonConvert.DeserializeObject<Network>(serializedResult, networkConverter);
            Assert.Equal(deserializedResult, mainNetwork);
        }

        [Fact]
        public void UInt256JsonConverterTest()
        {
            UInt256JsonConverter uInt256JsonConverter = new UInt256JsonConverter();
            uint256 n = new uint256();

            string serializedResult;
            uint256 deserializedResult;

            Assert.True(uInt256JsonConverter.CanConvert(typeof(uint256)));

            serializedResult = JsonConvert.SerializeObject(n, uInt256JsonConverter);
            Assert.Equal($"\"{n}\"", serializedResult);

            deserializedResult = JsonConvert.DeserializeObject<uint256>(serializedResult, uInt256JsonConverter);
            Assert.Equal(deserializedResult, n);
        }

        [Fact]
        public void DateTimeOffsetConverterTest()
        {
            DateTimeOffsetConverter dateTimeOffsetConverter = new DateTimeOffsetConverter();
            DateTimeOffset dateTimeOffset = new DateTimeOffset();

            string serializedResult;
            DateTimeOffset deserializedResult;

            Assert.True(dateTimeOffsetConverter.CanConvert(typeof(DateTimeOffset)));

            serializedResult = JsonConvert.SerializeObject(dateTimeOffset, dateTimeOffsetConverter);
            Assert.Equal($"\"{dateTimeOffset.ToUnixTimeSeconds()}\"", serializedResult);

            deserializedResult = JsonConvert.DeserializeObject<DateTimeOffset>(serializedResult, dateTimeOffsetConverter);
            Assert.Equal(deserializedResult, dateTimeOffset);
        }

        [Fact]
        public void ByteArrayConverterTest()
        {
            ByteArrayConverter byteArrayConverter = new ByteArrayConverter();
            byte[] bytes = {1, 3, 3, 7};

            string serializedResult;
            byte[] deserializedResult;

            Assert.True(byteArrayConverter.CanConvert(typeof(byte[])));

            serializedResult = JsonConvert.SerializeObject(bytes, byteArrayConverter);
            Assert.Equal($"\"{Convert.ToBase64String(bytes)}\"", serializedResult);

            deserializedResult = JsonConvert.DeserializeObject<byte[]>(serializedResult, byteArrayConverter);
            Assert.Equal(deserializedResult, bytes);
        }
    }
}
