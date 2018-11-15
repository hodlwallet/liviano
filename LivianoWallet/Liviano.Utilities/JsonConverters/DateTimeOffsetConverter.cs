using Newtonsoft.Json;
using System;

namespace Liviano.Utilities.JsonConverters
{
    public class DateTimeOffsetConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTimeOffset);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return DateTimeOffset.FromUnixTimeSeconds(long.Parse((string)reader.Value));
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((DateTimeOffset)value).ToUnixTimeSeconds().ToString());
        }
    }
}
