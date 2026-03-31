using System.Text.Json.Serialization;

namespace FormHandler.Api.Endpoints.Responses.Fields
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(TextFieldResponse), "text")]
    [JsonDerivedType(typeof(EmailFieldResponse), "email")]
    [JsonDerivedType(typeof(UrlFieldResponse), "url")]
    [JsonDerivedType(typeof(TextAreaFieldResponse), "textarea")]
    [JsonDerivedType(typeof(CheckboxFieldResponse), "checkbox")]
    [JsonDerivedType(typeof(NumberFieldResponse), "number")]
    [JsonDerivedType(typeof(SliderFieldResponse), "slider")]
    [JsonDerivedType(typeof(SelectFieldResponse), "select")]
    [JsonDerivedType(typeof(RadioGroupResponse), "radio")]
    [JsonDerivedType(typeof(CheckboxGroupResponse), "checkboxGroup")]
    public abstract class FieldResponse
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Label { get; init; }
        public bool IsRequired { get; init; }
        public IReadOnlyDictionary<string, string>? Attributes { get; init; }
    }
}
