using FormHandler.Api.Domain;

namespace FormHandler.Benchmarks
{
    public static class BenchmarkEmail
    {
        public static EmailAddress Email(string value) => EmailAddress.TryParse(value).Result!;
    }
}
