using System.Text.Json;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using FormHandler.Api.Domain.Submissions;

namespace FormHandler.Api.Endpoints
{
    public static class FieldValueMapper
    {
        public static IReadOnlyDictionary<string, FieldValue> FromFormEncoded(FormDefinition form, IFormCollection formData)
        {
            var fieldsByName = form.Fields.ToDictionary(field => field.Name);

            var result = formData.Keys.ToDictionary<string, string, FieldValue>(
                key => key,
                key =>
                {
                    var field = fieldsByName.GetValueOrDefault(key);
                    var values = formData[key].OfType<string>().ToArray();

                    return field switch
                    {
                        CheckboxFieldDefinition => new FieldValue.Toggle(true),
                        NumericFieldDefinition when values is [var text] && decimal.TryParse(text, out var number) => new FieldValue.Number(number),
                        NumericFieldDefinition => throw new JsonException($"Expected a number for field '{field.Name}'."),
                        ChoiceFieldDefinition => new FieldValue.Selection(values),
                        _ => new FieldValue.Text(values is [var single] ? single : values.FirstOrDefault() ?? string.Empty)
                    };
                });

            // HTML form submissions omit unchecked checkboxes entirely. Normalise them to
            // Toggle(false) so downstream consumers always see an explicit value.
            foreach (var field in form.Fields.OfType<CheckboxFieldDefinition>())
            {
                if (!result.ContainsKey(field.Name))
                {
                    result[field.Name] = new FieldValue.Toggle(false);
                }
            }

            return result;
        }

        public static IReadOnlyDictionary<string, FieldValue> FromJson(FormDefinition form, Dictionary<string, JsonElement> body)
        {
            var fieldsByName = form.Fields.ToDictionary(field => field.Name);

            var result = body.Where(pair => pair.Value.ValueKind != JsonValueKind.Null)
                .ToDictionary<KeyValuePair<string, JsonElement>, string, FieldValue>(
                pair => pair.Key,
                pair =>
                {
                    var field = fieldsByName.GetValueOrDefault(pair.Key);
                    var element = pair.Value;

                    return (field, element.ValueKind) switch
                    {
                        (CheckboxFieldDefinition, JsonValueKind.True or JsonValueKind.False) => new FieldValue.Toggle(element.GetBoolean()),
                        (CheckboxFieldDefinition, _) => throw new JsonException($"Expected boolean for field '{field!.Name}'."),

                        (NumericFieldDefinition, JsonValueKind.Number) => new FieldValue.Number(element.GetDecimal()),
                        (NumericFieldDefinition, _) => throw new JsonException($"Expected number for field '{field!.Name}'."),

                        (ChoiceFieldDefinition, JsonValueKind.Array) => new FieldValue.Selection([.. element.EnumerateArray().Select(item => item.GetString() ?? string.Empty)]),
                        (ChoiceFieldDefinition, JsonValueKind.String) => new FieldValue.Selection([element.GetString()!]),
                        (ChoiceFieldDefinition, _) => throw new JsonException($"Expected string or array for field '{field!.Name}'."),

                        (TextFieldDefinition or EmailFieldDefinition or UrlFieldDefinition or TextAreaFieldDefinition, JsonValueKind.String) => new FieldValue.Text(element.GetString()!),
                        (TextFieldDefinition or EmailFieldDefinition or UrlFieldDefinition or TextAreaFieldDefinition, _) => throw new JsonException($"Expected string for field '{field!.Name}'."),

                        (null, JsonValueKind.String) => new FieldValue.Text(element.GetString()!),
                        (null, JsonValueKind.Number) => new FieldValue.Number(element.GetDecimal()),
                        (null, JsonValueKind.True or JsonValueKind.False) => new FieldValue.Toggle(element.GetBoolean()),
                        (null, JsonValueKind.Array) => new FieldValue.Selection([.. element.EnumerateArray().Select(item => item.GetString() ?? string.Empty)]),
                        _ => throw new JsonException($"Unexpected JSON value for field '{field?.Name}'.")
                    };
                });

            // JSON submissions may omit unchecked checkboxes. Normalise them to Toggle(false)
            // so downstream consumers always see an explicit value.
            foreach (var field in form.Fields.OfType<CheckboxFieldDefinition>())
            {
                if (!result.ContainsKey(field.Name))
                {
                    result[field.Name] = new FieldValue.Toggle(false);
                }
            }

            return result;
        }
    }
}