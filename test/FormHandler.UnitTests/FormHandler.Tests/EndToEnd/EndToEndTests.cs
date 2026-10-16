using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FormHandler.Api;
using FormHandler.Tests.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace FormHandler.Tests.EndToEnd
{
    public sealed class Smtp4DevContainerFixture : IAsyncLifetime
    {
        private IContainer container = null!;

        public int SmtpPort { get; private set; }
        public int ApiPort { get; private set; }

        public async ValueTask InitializeAsync()
        {
            this.container = new ContainerBuilder("rnwood/smtp4dev:v3")
                .WithPortBinding(25, true)
                .WithPortBinding(80, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request
                        .ForPath("/api/Messages")
                        .ForPort(80)))
                .Build();

            await this.container.StartAsync();
            this.SmtpPort = this.container.GetMappedPublicPort(25);
            this.ApiPort = this.container.GetMappedPublicPort(80);
        }

        public async ValueTask DisposeAsync()
        {
            await this.container.DisposeAsync();
        }
    }

    [CollectionDefinition("EndToEnd")]
    public sealed class EndToEndCollection : ICollectionFixture<EndToEndContainerFixture>
    { }

    public sealed class EndToEndContainerFixture : IAsyncLifetime
    {
        public PostgresContainerFixture Postgres { get; } = new();
        public Smtp4DevContainerFixture Smtp4Dev { get; } = new();

        public async ValueTask InitializeAsync()
        {
            await Task.WhenAll(
                this.Postgres.InitializeAsync().AsTask(),
                this.Smtp4Dev.InitializeAsync().AsTask());
        }

        public async ValueTask DisposeAsync()
        {
            await Task.WhenAll(
                this.Postgres.DisposeAsync().AsTask(),
                this.Smtp4Dev.DisposeAsync().AsTask());
        }
    }

    public sealed class EndToEndFactory : WebApplicationFactory<Program>
    {
        private readonly EndToEndContainerFixture fixture;

        public EndToEndFactory(EndToEndContainerFixture fixture)
        {
            this.fixture = fixture;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Default", this.fixture.Postgres.ConnectionString);
            builder.UseSetting("Email:From:Address", "noreply@test.example");
            builder.UseSetting("Email:From:Name", "Test");
            builder.UseSetting("Email:Smtp:Host", "localhost");
            builder.UseSetting("Email:Smtp:Port", this.fixture.Smtp4Dev.SmtpPort.ToString());
            builder.UseSetting("Email:Smtp:Username", "test");
            builder.UseSetting("Email:Smtp:Password", "test");
            builder.UseSetting("Outbox:PollInterval", "00:00:01");
            builder.UseSetting("FormDefinitions:Directory", Path.Combine(AppContext.BaseDirectory, "Forms"));
        }
    }

    [Collection("EndToEnd")]
    public sealed class EndToEndTests : IAsyncLifetime
    {
        private readonly EndToEndContainerFixture fixture;
        private EndToEndFactory factory = null!;
        private HttpClient client = null!;
        private HttpClient smtp4DevClient = null!;

        public EndToEndTests(EndToEndContainerFixture fixture)
        {
            this.fixture = fixture;
        }

        public async ValueTask InitializeAsync()
        {
            this.factory = new EndToEndFactory(this.fixture);
            this.client = this.factory.CreateClient();
            this.smtp4DevClient = new HttpClient
            {
                BaseAddress = new Uri($"http://localhost:{this.fixture.Smtp4Dev.ApiPort}"),
            };

            await using var connection = await this.fixture.Postgres.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "TRUNCATE form_submissions CASCADE";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

            await this.smtp4DevClient.DeleteAsync("/api/Messages/*", TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            this.client.Dispose();
            this.smtp4DevClient.Dispose();
            await this.factory.DisposeAsync();
        }

        // GET /forms/{formId}

        [Fact]
        public async Task GetKnownFormReturnsFormDefinition()
        {
            var response = await this.client.GetAsync("/forms/contact", TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            body.GetProperty("id").GetString().ShouldBe("contact");
        }

        [Fact]
        public async Task GetUnknownFormReturnsNotFound()
        {
            var response = await this.client.GetAsync("/forms/does-not-exist", TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        // POST /forms/{formId}/submissions — JSON

        [Fact]
        public async Task SubmitJsonToUnknownFormReturnsNotFound()
        {
            var response = await this.client.PostAsync(
                "/forms/does-not-exist/submissions",
                this.Json("""{"name":"Alice","email":"alice@example.com","message":"Hello"}"""),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task SubmitJsonWithMissingRequiredFieldReturnsValidationError()
        {
            var response = await this.client.PostAsync(
                "/forms/contact/submissions",
                this.Json("""{"name":"Alice"}"""),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            body.GetProperty("errors").TryGetProperty("email", out _).ShouldBeTrue();
            body.GetProperty("errors").TryGetProperty("message", out _).ShouldBeTrue();
        }

        [Fact]
        public async Task SubmitJsonWithUnknownFieldReturnsValidationError()
        {
            var response = await this.client.PostAsync(
                "/forms/contact/submissions",
                this.Json("""{"name":"Alice","email":"alice@example.com","message":"Hello","unknown_field":"bad"}"""),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SubmitJsonWithEmptyBodyReturnsBadRequest()
        {
            var response = await this.client.PostAsync(
                "/forms/contact/submissions",
                new StringContent("", Encoding.UTF8, "application/json"),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SubmitJsonWithValidDataIsEmailedViaOutbox()
        {
            var response = await this.client.PostAsync(
                "/forms/contact/submissions",
                this.Json("""{"name":"Alice","email":"alice@example.com","message":"Hello from JSON."}"""),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            var message = await this.PollForMessageWithSubjectAsync(
                "New submission for form \"Contact Us\"",
                TestContext.Current.CancellationToken);

            message.ShouldNotBeNull("Expected an email to arrive in smtp4dev within the timeout.");
            message.Value.GetProperty("to")
                .EnumerateArray()
                .Select(element => element.GetString())
                .ShouldContain(address => address != null && address.Contains("hello@example.com"));
        }

        // POST /forms/{formId}/submissions — form-encoded

        [Fact]
        public async Task SubmitFormEncodedToUnknownFormReturnsNotFound()
        {
            var response = await this.client.PostAsync(
                "/forms/does-not-exist/submissions",
                new FormUrlEncodedContent([new KeyValuePair<string, string>("name", "Alice")]),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task SubmitFormEncodedWithMissingRequiredFieldReturnsValidationError()
        {
            var response = await this.client.PostAsync(
                "/forms/contact/submissions",
                new FormUrlEncodedContent([new KeyValuePair<string, string>("name", "Alice")]),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            body.GetProperty("errors").TryGetProperty("email", out _).ShouldBeTrue();
            body.GetProperty("errors").TryGetProperty("message", out _).ShouldBeTrue();
        }

        [Fact]
        public async Task SubmitFormEncodedWithUnknownFieldReturnsValidationError()
        {
            var response = await this.client.PostAsync(
                "/forms/contact/submissions",
                new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("name", "Alice"),
                    new KeyValuePair<string, string>("email", "alice@example.com"),
                    new KeyValuePair<string, string>("message", "Hello"),
                    new KeyValuePair<string, string>("unknown_field", "bad"),
                ]),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SubmitFormEncodedWithValidDataIsEmailedViaOutbox()
        {
            var response = await this.client.PostAsync(
                "/forms/contact/submissions",
                new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("name", "Alice"),
                    new KeyValuePair<string, string>("email", "alice@example.com"),
                    new KeyValuePair<string, string>("message", "Hello from form-encoded."),
                ]),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            var message = await this.PollForMessageWithSubjectAsync(
                "New submission for form \"Contact Us\"",
                TestContext.Current.CancellationToken);

            message.ShouldNotBeNull("Expected an email to arrive in smtp4dev within the timeout.");
            message.Value.GetProperty("to")
                .EnumerateArray()
                .Select(element => element.GetString())
                .ShouldContain(address => address != null && address.Contains("hello@example.com"));
        }

        // Content-type

        [Fact]
        public async Task SubmitWithWrongContentTypeReturnsUnsupportedMediaType()
        {
            var response = await this.client.PostAsync(
                "/forms/contact/submissions",
                new StringContent("""{"name":"Alice","email":"alice@example.com","message":"Hello"}""", Encoding.UTF8, "text/plain"),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        }

        private async Task<JsonElement?> PollForMessageWithSubjectAsync(string subject, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);

            while (DateTime.UtcNow < deadline)
            {
                var response = await this.smtp4DevClient.GetFromJsonAsync<JsonElement>("/api/Messages", cancellationToken);
                var match = response.GetProperty("results")
                    .EnumerateArray()
                    .FirstOrDefault(message => message.GetProperty("subject").GetString() == subject);

                if (match.ValueKind != JsonValueKind.Undefined)
                {
                    return match;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }

            return null;
        }

        private StringContent Json(string json) =>
            new StringContent(json, Encoding.UTF8, "application/json");
    }
}
