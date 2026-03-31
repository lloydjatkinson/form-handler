using System.Text.Json;
using System.Text.Json.Serialization;
using FormHandler.Api.Domain.Submissions;

namespace FormHandler.Api.Infrastructure.Persistence
{
    public sealed class FieldValueConverter : JsonConverter<FieldValue>
    {
        public override FieldValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new FieldValue.Text(reader.GetString()!);
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<string>();

                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        list.Add(reader.GetString()!);
                    }
                    else if (reader.TokenType != JsonTokenType.Null)
                    {
                        throw new JsonException($"Expected string value in array, got {reader.TokenType}.");
                    }
                }

                return new FieldValue.Selection(list);
            }

            if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
            {
                return new FieldValue.Toggle(reader.GetBoolean());
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                return new FieldValue.Number(reader.GetDecimal());
            }

            throw new JsonException($"Expected string, array, boolean, or number, got {reader.TokenType}.");
        }

        public override void Write(Utf8JsonWriter writer, FieldValue value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case FieldValue.Text text:
                    writer.WriteStringValue(text.Value);
                    break;

                case FieldValue.Selection selection:
                    writer.WriteStartArray();
                    foreach (var v in selection.Values)
                    {
                        writer.WriteStringValue(v);
                    }

                    writer.WriteEndArray();
                    break;

                case FieldValue.Toggle toggle:
                    writer.WriteBooleanValue(toggle.Value);
                    break;

                case FieldValue.Number n:
                    writer.WriteNumberValue(n.Value);
                    break;
            }
        }
    }
}