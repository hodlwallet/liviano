using System;

using Newtonsoft.Json;

using NBitcoin;

using Encoders = NBitcoin.DataEncoders.Encoders;

namespace Liviano.Utilities.JsonConverters
{
    public class PrivateKeyConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Key);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var bytes = Encoders.Hex.DecodeData((string)reader.Value);

            return new Key(bytes);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var hex = Encoders.Hex.EncodeData(((Key)value).ToBytes());

            writer.WriteValue(hex);
        }
    }
}
