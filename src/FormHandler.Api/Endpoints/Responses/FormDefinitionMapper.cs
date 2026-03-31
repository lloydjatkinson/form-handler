using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using FormHandler.Api.Endpoints.Responses.Fields;

namespace FormHandler.Api.Endpoints.Responses
{
    public static class FormDefinitionMapper
    {
        public static FormDefinitionResponse ToResponse(FormDefinition form) =>
            new FormDefinitionResponse
            {
                Id = form.Id,
                Name = form.Name,
                Fields = form.Fields.Select(MapField).ToList()
            };

        private static FieldResponse MapField(FieldDefinition definition) => definition switch
        {
            TextFieldDefinition field => new TextFieldResponse
            {
                Id = field.Id,
                Name = field.Name,
                Label = field.Label,
                IsRequired = field.IsRequired,
                Attributes = field.Attributes.Count > 0 ? field.Attributes : null,
                MaxLength = field.MaxLength,
                Placeholder = field.Placeholder
            },
            EmailFieldDefinition field => new EmailFieldResponse
            {
                Id = field.Id,
                Name = field.Name,
                Label = field.Label,
                IsRequired = field.IsRequired,
                Attributes = field.Attributes.Count > 0 ? field.Attributes : null,
                Placeholder = field.Placeholder
            },
            UrlFieldDefinition field => new UrlFieldResponse
            {
                Id = field.Id,
                Name = field.Name,
                Label = field.Label,
                IsRequired = field.IsRequired,
                Attributes = field.Attributes.Count > 0 ? field.Attributes : null,
                Placeholder = field.Placeholder
            },
            TextAreaFieldDefinition field => new TextAreaFieldResponse
            {
                Id = field.Id,
                Name = field.Name,
                Label = field.Label,
                IsRequired = field.IsRequired,
                Attributes = field.Attributes.Count > 0 ? field.Attributes : null,
                Rows = field.Rows,
                Cols = field.Cols
            },
            CheckboxFieldDefinition field => new CheckboxFieldResponse
            {
                Id = field.Id,
                Name = field.Name,
                Label = field.Label,
                IsRequired = field.IsRequired,
                Attributes = field.Attributes.Count > 0 ? field.Attributes : null
            },
            SelectFieldDefinition field => new SelectFieldResponse
            {
                Id = field.Id,
                Name = field.Name,
                Label = field.Label,
                IsRequired = field.IsRequired,
                Attributes = field.Attributes.Count > 0 ? field.Attributes : null,
                Options = MapOptions(field)
            },
            RadioGroupDefinition field => new RadioGroupResponse
            {
                Id = field.Id,
                Name = field.Name,
                Label = field.Label,
                IsRequired = field.IsRequired,
                Attributes = field.Attributes.Count > 0 ? field.Attributes : null,
                Options = MapOptions(field)
            },
            CheckboxGroupDefinition field => new CheckboxGroupResponse
            {
                Id = field.Id,
                Name = field.Name,
                Label = field.Label,
                IsRequired = field.IsRequired,
                Attributes = field.Attributes.Count > 0 ? field.Attributes : null,
                Options = MapOptions(field)
            },
            NumberFieldDefinition field => new NumberFieldResponse
            {
                Id = field.Id,
                Name = field.Name,
                Label = field.Label,
                IsRequired = field.IsRequired,
                Attributes = field.Attributes.Count > 0 ? field.Attributes : null,
                Min = field.Min,
                Max = field.Max,
                Step = field.Step
            },
            SliderFieldDefinition field => new SliderFieldResponse
            {
                Id = field.Id,
                Name = field.Name,
                Label = field.Label,
                IsRequired = field.IsRequired,
                Attributes = field.Attributes.Count > 0 ? field.Attributes : null,
                Min = field.Min,
                Max = field.Max,
                Step = field.Step
            },
            _ => throw new InvalidOperationException($"Unknown field type: {definition.GetType().Name}")
        };

        private static IReadOnlyList<FieldOptionResponse> MapOptions(ChoiceFieldDefinition field) =>
            field.Options.Select(o => new FieldOptionResponse
            {
                Value = o.Value,
                Label = o.Label,
                IsDisabled = o.IsDisabled
            }).ToList();
    }
}