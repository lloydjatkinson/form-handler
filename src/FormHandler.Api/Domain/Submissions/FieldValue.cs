namespace FormHandler.Api.Domain.Submissions
{
    public abstract record FieldValue
    {
        public sealed record Text(string Value) : FieldValue;
        public sealed record Selection(IReadOnlyList<string> Values) : FieldValue;
        public sealed record Toggle(bool Value) : FieldValue;
        public sealed record Number(decimal Value) : FieldValue;
    }
}