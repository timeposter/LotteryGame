
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LotteryAdminSystem.Converter
{
    public class JsonNullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            string str = reader.GetString()?.Trim();
            if (string.IsNullOrEmpty(str))
                return null;

            if (DateTime.TryParse(str, out DateTime dt))
                return dt;

            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            else
                writer.WriteNullValue();
        }
    }
}