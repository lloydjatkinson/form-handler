using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;

namespace FormHandler.Api.Infrastructure.Forms
{
    public static class FormDescriptorMapper
    {
        public static (FormDefinition? Result, IReadOnlyList<string> Errors) ToFormDefinition(
            FormDescriptor descriptor)
        {
            var errors = new List<string>();
            var fields = new List<FieldDefinition>();

            for (int fieldIndex = 0; fieldIndex < descriptor.Fields.Count; fieldIndex++)
            {
                var (field, fieldErrors) = ToFieldDefinition(descriptor.Fields[fieldIndex], fieldIndex);

                foreach (var error in fieldErrors)
                {
                    errors.Add(error);
                }

                if (field is not null)
                {
                    fields.Add(field);
                }
            }

            // Form-level presence and the cross-field replyTo rule are the aggregate root's
            // invariants, so they live in FormDefinition.Create. The mapper just hands it the parts.
            var (emailOptions, emailErrors) = ToEmailOptions(descriptor.EmailOptions);

            foreach (var error in emailErrors)
            {
                errors.Add(error);
            }

            var (form, formErrors) = FormDefinition.Create(
                descriptor.Id, descriptor.Name, emailOptions, fields, descriptor.Attributes);

            foreach (var error in formErrors)
            {
                errors.Add(error);
            }

            if (errors.Count > 0)
            {
                return (null, errors);
            }

            return (form, errors);
        }

        private static (FieldDefinition? Result, IReadOnlyList<string> Errors) ToFieldDefinition(
            FieldDescriptor descriptor, int index)
        {
            var errors = new List<string>();
            var position = descriptor.Id is not null ? $"field '{descriptor.Id}'" : $"field at index {index}";

            if (string.IsNullOrWhiteSpace(descriptor.Id))
            {
                errors.Add($"'id' is required on {position}.");
            }

            if (string.IsNullOrWhiteSpace(descriptor.Name))
            {
                errors.Add($"'name' is required on {position}.");
            }

            if (string.IsNullOrWhiteSpace(descriptor.Label))
            {
                errors.Add($"'label' is required on {position}.");
            }

            if (descriptor is NumericFieldDescriptor numeric
                && numeric.Min is decimal minValue
                && numeric.Max is decimal maxValue
                && minValue > maxValue)
            {
                errors.Add($"'min' must not be greater than 'max' on {position}.");
            }

            if (errors.Count > 0)
            {
                return (null, errors);
            }

            var id = descriptor.Id!;
            var label = descriptor.Label!;
            var name = descriptor.Name!;

            FieldDefinition? field = descriptor switch
            {
                TextFieldDescriptor text => new TextFieldDefinition
                {
                    Id = id,
                    Name = name,
                    Label = label,
                    IsRequired = text.IsRequired,
                    Attributes = text.Attributes,
                    MaxLength = text.MaxLength,
                    Placeholder = text.Placeholder
                },
                TextAreaFieldDescriptor textArea => new TextAreaFieldDefinition
                {
                    Id = id,
                    Name = name,
                    Label = label,
                    IsRequired = textArea.IsRequired,
                    Attributes = textArea.Attributes,
                    Rows = textArea.Rows,
                    Cols = textArea.Cols
                },
                EmailFieldDescriptor email => new EmailFieldDefinition
                {
                    Id = id,
                    Name = name,
                    Label = label,
                    IsRequired = email.IsRequired,
                    Attributes = email.Attributes,
                    Placeholder = email.Placeholder
                },
                UrlFieldDescriptor url => new UrlFieldDefinition
                {
                    Id = id,
                    Name = name,
                    Label = label,
                    IsRequired = url.IsRequired,
                    Attributes = url.Attributes,
                    Placeholder = url.Placeholder
                },
                CheckboxFieldDescriptor checkbox => new CheckboxFieldDefinition
                {
                    Id = id,
                    Name = name,
                    Label = label,
                    IsRequired = checkbox.IsRequired,
                    Attributes = checkbox.Attributes
                },
                NumberFieldDescriptor number => new NumberFieldDefinition
                {
                    Id = id,
                    Name = name,
                    Label = label,
                    IsRequired = number.IsRequired,
                    Attributes = number.Attributes,
                    Min = number.Min,
                    Max = number.Max,
                    Step = number.Step
                },
                SliderFieldDescriptor slider => new SliderFieldDefinition
                {
                    Id = id,
                    Name = name,
                    Label = label,
                    IsRequired = slider.IsRequired,
                    Attributes = slider.Attributes,
                    Min = slider.Min,
                    Max = slider.Max,
                    Step = slider.Step
                },
                SelectFieldDescriptor select => MapChoiceField(
                    select, id, name, label, errors, position,
                    (baseProperties, options) => new SelectFieldDefinition
                    {
                        Id = baseProperties.Id,
                        Name = baseProperties.Name,
                        Label = baseProperties.Label,
                        IsRequired = baseProperties.IsRequired,
                        Attributes = baseProperties.Attributes,
                        Options = options
                    }),
                RadioGroupDescriptor radio => MapChoiceField(
                    radio, id, name, label, errors, position,
                    (baseProperties, options) => new RadioGroupDefinition
                    {
                        Id = baseProperties.Id,
                        Name = baseProperties.Name,
                        Label = baseProperties.Label,
                        IsRequired = baseProperties.IsRequired,
                        Attributes = baseProperties.Attributes,
                        Options = options
                    }),
                CheckboxGroupDescriptor checkboxGroup => MapChoiceField(
                    checkboxGroup, id, name, label, errors, position,
                    (baseProperties, options) => new CheckboxGroupDefinition
                    {
                        Id = baseProperties.Id,
                        Name = baseProperties.Name,
                        Label = baseProperties.Label,
                        IsRequired = baseProperties.IsRequired,
                        Attributes = baseProperties.Attributes,
                        Options = options
                    }),
                _ => null
            };

            if (field is null && errors.Count == 0)
            {
                errors.Add($"Unknown field descriptor on {position}.");
            }

            if (errors.Count > 0)
            {
                return (null, errors);
            }

            return (field, errors);
        }

        private sealed record ChoiceBaseProperties(
            string Id, string Name, string Label, bool IsRequired,
            IReadOnlyDictionary<string, string> Attributes);

        private static TFieldDefinition? MapChoiceField<TFieldDefinition>(
            ChoiceFieldDescriptor descriptor,
            string id,
            string name,
            string label,
            List<string> errors,
            string position,
            Func<ChoiceBaseProperties, IReadOnlyList<FieldOption>, TFieldDefinition> factory)
            where TFieldDefinition : ChoiceFieldDefinition
        {
            if (descriptor.Options.Count == 0)
            {
                errors.Add($"'options' must not be empty on {position}.");
                return null;
            }

            var options = descriptor.Options
                .Select(option => new FieldOption(option.Value ?? string.Empty, option.Label ?? string.Empty, option.IsDisabled))
                .ToList();

            var baseProperties = new ChoiceBaseProperties(
                id,
                name,
                label,
                descriptor.IsRequired,
                descriptor.Attributes);

            return factory(baseProperties, options);
        }

        private static (FormSubmissionEmailOptions? Options, IReadOnlyList<string> Errors) ToEmailOptions(EmailOptionsDescriptor? descriptor)
        {
            if (descriptor is null)
            {
                return (null, ["'emailOptions' is required."]);
            }

            var (to, toError) = EmailAddress.TryParse(descriptor.To);
            if (toError is not null)
            {
                return (null, [$"'emailOptions.to' {toError}."]);
            }

            return (new FormSubmissionEmailOptions
            {
                To = to!,
                Cc = descriptor.Cc,
                Bcc = descriptor.Bcc,
                ReplyToFieldName = descriptor.ReplyToFieldName,
                Branding = new FormSubmissionEmailBranding
                {
                    PrimaryColor = descriptor.Branding?.PrimaryColor ?? "#0066cc",
                    SecondaryColor = descriptor.Branding?.SecondaryColor ?? "#333333"
                }
            }, []);
        }
    }
}
