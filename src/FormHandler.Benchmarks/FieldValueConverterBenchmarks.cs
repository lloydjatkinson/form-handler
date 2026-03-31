using System.Text.Json;
using BenchmarkDotNet.Attributes;
using FormHandler.Api.Domain.Submissions;
using FormHandler.Api.Infrastructure.Persistence;

namespace FormHandler.Benchmarks
{
    [MemoryDiagnoser]
    public class FieldValueConverterBenchmarks
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            Converters = { new FieldValueConverter() }
        };

        private static readonly byte[] TextJson = "\"hello world\""u8.ToArray();
        private static readonly byte[] SelectionSingleJson = "[\"apples\"]"u8.ToArray();
        private static readonly byte[] SelectionMultipleJson = "[\"apples\",\"oranges\",\"bananas\"]"u8.ToArray();
        private static readonly byte[] ToggleJson = "true"u8.ToArray();
        private static readonly byte[] NumberJson = "42.5"u8.ToArray();

        private static readonly FieldValue TextValue = new FieldValue.Text("hello world");
        private static readonly FieldValue SelectionSingleValue = new FieldValue.Selection(["apples"]);
        private static readonly FieldValue SelectionMultipleValue = new FieldValue.Selection(["apples", "oranges", "bananas"]);
        private static readonly FieldValue ToggleValue = new FieldValue.Toggle(true);
        private static readonly FieldValue NumberValue = new FieldValue.Number(42.5m);

        [Benchmark] public FieldValue ReadText() => JsonSerializer.Deserialize<FieldValue>(TextJson, Options)!;

        [Benchmark] public FieldValue ReadSelectionSingle() => JsonSerializer.Deserialize<FieldValue>(SelectionSingleJson, Options)!;

        [Benchmark] public FieldValue ReadSelectionMultiple() => JsonSerializer.Deserialize<FieldValue>(SelectionMultipleJson, Options)!;

        [Benchmark] public FieldValue ReadToggle() => JsonSerializer.Deserialize<FieldValue>(ToggleJson, Options)!;

        [Benchmark] public FieldValue ReadNumber() => JsonSerializer.Deserialize<FieldValue>(NumberJson, Options)!;

        [Benchmark] public string WriteText() => JsonSerializer.Serialize(TextValue, Options);

        [Benchmark] public string WriteSelectionSingle() => JsonSerializer.Serialize(SelectionSingleValue, Options);

        [Benchmark] public string WriteSelectionMultiple() => JsonSerializer.Serialize(SelectionMultipleValue, Options);

        [Benchmark] public string WriteToggle() => JsonSerializer.Serialize(ToggleValue, Options);

        [Benchmark] public string WriteNumber() => JsonSerializer.Serialize(NumberValue, Options);
    }
}