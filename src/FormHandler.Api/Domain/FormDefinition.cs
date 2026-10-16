using FormHandler.Api.Domain.Fields;

namespace FormHandler.Api.Domain
{
    public sealed class FormDefinition
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required FormSubmissionEmailOptions EmailOptions { get; init; }
        public required IReadOnlyList<FieldDefinition> Fields { get; init; }
        public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();

        private FormDefinition()
        {
        }

        public static (FormDefinition? Form, IReadOnlyList<string> Errors) Create(
            string? id, string? name, FormSubmissionEmailOptions? emailOptions,
            IReadOnlyList<FieldDefinition> fields, IReadOnlyDictionary<string, string>? attributes = null)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add("'id' is required.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("'name' is required.");
            }

            var replyToFieldName = emailOptions?.ReplyToFieldName;
            if (replyToFieldName is not null)
            {
                if (string.IsNullOrWhiteSpace(replyToFieldName))
                {
                    errors.Add("'emailOptions.replyToFieldName' must not be blank.");
                }
                else
                {
                    var replyToField = fields.FirstOrDefault(field => field.Name == replyToFieldName);

                    if (replyToField is null)
                    {
                        errors.Add($"replyToFieldName '{replyToFieldName}' does not match any field.");
                    }
                    else if (replyToField is not EmailFieldDefinition)
                    {
                        errors.Add($"replyToFieldName '{replyToFieldName}' must reference an email field.");
                    }
                }
            }

            foreach (var duplicateName in fields.GroupBy(field => field.Name).Where(group => group.Count() > 1).Select(group => group.Key))
            {
                errors.Add($"duplicate field name '{duplicateName}'.");
            }

            if (errors.Count > 0 || emailOptions is null)
            {
                return (null, errors);
            }

            return (new FormDefinition
            {
                Id = id!,
                Name = name!,
                EmailOptions = emailOptions,
                Fields = fields,
                Attributes = attributes ?? new Dictionary<string, string>()
            }, errors);
        }
    }
}
