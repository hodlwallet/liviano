using System;

using Newtonsoft.Json;

using NBitcoin;

namespace Liviano.Utilities.JsonConverters
{
    public class PublicKeyConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PubKey);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return new PubKey((string)reader.Value);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((PubKey)value).ToHex());
        }
    }
}
