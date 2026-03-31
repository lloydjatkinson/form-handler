using FormHandler.Api.Domain.Fields;

namespace FormHandler.Api.Domain.Submissions
{
    public sealed class FormSubmissionValidator
    {
        private static bool IsEmpty(FieldDefinition field, FieldValue value)
        {
            if (field is CheckboxFieldDefinition)
            {
                // Unchecked checkboxes are normalised to Toggle(false) by FieldValueMapper
                // (form-urlencoded omits them; JSON may send false explicitly). Either way,
                // Toggle(false) means unchecked, which is treated as empty for required validation.
                return value is null or FieldValue.Toggle { Value: false };
            }

            return value switch
            {
                FieldValue.Text t => string.IsNullOrWhiteSpace(t.Value),
                FieldValue.Selection s => s.Values.Count == 0 || s.Values.All(string.IsNullOrWhiteSpace),
                FieldValue.Toggle => false,
                FieldValue.Number => false,
                _ => true
            };
        }

        private static IEnumerable<string> ValidateField(FieldDefinition field, FieldValue value) =>
            field switch
            {
                ChoiceFieldDefinition choiceField => ValidateChoiceField(choiceField, (FieldValue.Selection)value),
                NumericFieldDefinition numericField => ValidateNumericField(numericField, (FieldValue.Number)value),
                _ => []
            };

        private static IEnumerable<string> ValidateChoiceField(ChoiceFieldDefinition field, FieldValue.Selection selection)
        {
            if (field is not CheckboxGroupDefinition && selection.Values.Count != 1)
            {
                yield return $"{field.Label} must have exactly one selection.";
                yield break;
            }

            var validValues = field.Options.Select(o => o.Value).ToHashSet();

            if (selection.Values.Any(v => !validValues.Contains(v)))
            {
                yield return $"{field.Label} contains an invalid selection.";
            }
        }

        private static IEnumerable<string> ValidateNumericField(NumericFieldDefinition field, FieldValue.Number value)
        {
            if (field.Min.HasValue && value.Value < field.Min.Value)
            {
                yield return $"{field.Label} must be at least {field.Min.Value}.";
            }

            if (field.Max.HasValue && value.Value > field.Max.Value)
            {
                yield return $"{field.Label} must be at most {field.Max.Value}.";
            }
        }

        public FormSubmissionResult Validate(FormDefinition form, FormSubmission submission)
        {
            var errors = new List<FieldValidationError>();
            var knownFieldNames = form.Fields.Select(field => field.Name).ToHashSet();

            foreach (var field in form.Fields)
            {
                bool hasValue = submission.Values.TryGetValue(field.Name, out var value);
                bool isEmpty = !hasValue || IsEmpty(field, value!);

                if (field.IsRequired && isEmpty)
                {
                    errors.Add(new FieldValidationError
                    {
                        FieldName = field.Name,
                        Message = $"{field.Label} is required."
                    });

                    continue;
                }

                if (isEmpty)
                {
                    continue;
                }

                foreach (var message in ValidateField(field, value!))
                {
                    errors.Add(new FieldValidationError
                    {
                        FieldName = field.Name,
                        Message = message
                    });
                }
            }

            foreach (var key in submission.Values.Keys.Where(key => !knownFieldNames.Contains(key)))
            {
                errors.Add(new FieldValidationError
                {
                    FieldName = key,
                    Message = $"'{key}' is not a known field."
                });
            }

            return errors.Count > 0
                ? new FormSubmissionResult.Invalid(errors)
                : new FormSubmissionResult.Success();
        }
    }
}