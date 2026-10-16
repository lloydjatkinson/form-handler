using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FormHandler.Api;
using FormHandler.Api.Application.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace FormHandler.Tests.Endpoints
{
    public sealed class FormEndpointsFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IFormDefinitionStore>(RegistrationFormDefinitionStore.Create());
                services.AddSingleton<TimeProvider>(new FakeTimeProvider());
            });
        }
    }

    [CollectionDefinition(nameof(FormEndpointsTests), DisableParallelization = true)]
    public sealed class FormEndpointsCollection
    { }

    [Collection(nameof(FormEndpointsTests))]
    public sealed class FormEndpointsTests : IClassFixture<FormEndpointsFactory>
    {
        private readonly HttpClient client;

        public FormEndpointsTests(FormEndpointsFactory factory)
        {
            this.client = factory.CreateClient();
        }

        // GET /forms/{formId}

        [Fact]
        public async Task GetFormReturnsOkForKnownFormId()
        {
            var response = await this.client.GetAsync("/forms/registration", TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetFormReturnsNotFoundForUnknownFormId()
        {
            var response = await this.client.GetAsync("/forms/unknown", TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        // POST /forms/{formId}/submissions — JSON, valid

        [Fact]
        public async Task SubmitJsonWithRequiredFieldsOnlyReturns201()
        {
            var content = this.Json("""{"name":"Alice","email":"alice@example.com","enquiryType":"sales","interests":["product"]}""");

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.Created);
        }

        [Fact]
        public async Task SubmitJsonWithMultipleInterestsReturns201()
        {
            var content = this.Json("""{"name":"Alice","email":"alice@example.com","enquiryType":"sales","interests":["product","pricing"]}""");

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.Created);
        }

        [Fact]
        public async Task SubmitJsonWithAllOptionalFieldsReturns201()
        {
            var content = this.Json("""{"name":"Alice","company":"Acme","email":"alice@example.com","website":"https://acme.example","enquiryType":"sales","interests":["product"],"preferredContact":"email","availability":["monday","tuesday"],"teamSize":50,"satisfaction":9,"comments":"Please reply soon.","newsletter":true}""");

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.Created);
        }

        // POST — JSON, bad request (400)

        [Fact]
        public async Task SubmitJsonWithEmptyBodyReturns400()
        {
            var content = new StringContent("", Encoding.UTF8, "application/json");

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }

        // POST — JSON, validation errors (400 with errors body)

        [Fact]
        public async Task SubmitJsonWithInvalidOptionReturns400()
        {
            var content = this.Json("""{"name":"Alice","email":"alice@example.com","enquiryType":"invalid-option","interests":["product"]}""");

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SubmitJsonWithInvalidOptionReturnsErrorsForAffectedField()
        {
            var content = this.Json("""{"name":"Alice","email":"alice@example.com","enquiryType":"invalid-option","interests":["product"]}""");

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            body.GetProperty("errors").GetProperty("enquiryType").GetArrayLength().ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task SubmitJsonWithMultipleValuesOnRadioReturns400()
        {
            var content = this.Json("""{"name":"Alice","email":"alice@example.com","enquiryType":["sales","support"],"interests":["product"]}""");

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SubmitJsonWithUnknownFieldReturns400()
        {
            var content = this.Json("""{"name":"Alice","email":"alice@example.com","enquiryType":"sales","interests":["product"],"sneaky_field":"should be rejected"}""");

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SubmitJsonWithRequiredFieldMissingReturns400()
        {
            var content = this.Json("""{"name":"Alice"}""");

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SubmitJsonWithNullOnRequiredFieldReturns400()
        {
            var content = this.Json("""{"name":"Alice","email":"alice@example.com","enquiryType":null,"interests":["product"]}""");

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }

        // POST — JSON, not found

        [Fact]
        public async Task SubmitJsonToUnknownFormReturns404()
        {
            var content = this.Json("""{"name":"Alice","email":"alice@example.com","enquiryType":"sales","interests":["product"]}""");

            var response = await this.client.PostAsync("/forms/unknown/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.NotFound);
        }

        // POST — wrong content type

        [Fact]
        public async Task SubmitWithWrongContentTypeReturns415()
        {
            var content = new StringContent(
                """{"name":"Alice","email":"alice@example.com","enquiryType":"sales","interests":["product"]}""",
                Encoding.UTF8,
                "text/plain");

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.UnsupportedMediaType);
        }

        // POST — form-encoded

        [Fact]
        public async Task SubmitFormEncodedWithValidValuesReturns201()
        {
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("name", "Alice"),
                new KeyValuePair<string, string>("email", "alice@example.com"),
                new KeyValuePair<string, string>("enquiryType", "sales"),
                new KeyValuePair<string, string>("interests", "product"),
            ]);

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.Created);
        }

        [Fact]
        public async Task SubmitFormEncodedWithInvalidOptionReturns400()
        {
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("name", "Alice"),
                new KeyValuePair<string, string>("email", "alice@example.com"),
                new KeyValuePair<string, string>("enquiryType", "invalid-option"),
                new KeyValuePair<string, string>("interests", "product"),
            ]);

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SubmitFormEncodedToUnknownFormReturns404()
        {
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("name", "Alice"),
            ]);

            var response = await this.client.PostAsync("/forms/unknown/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task SubmitFormEncodedWithRequiredFieldMissingReturns400()
        {
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("company", "Acme"),
            ]);

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SubmitFormEncodedWithUnknownFieldReturns400()
        {
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("name", "Alice"),
                new KeyValuePair<string, string>("email", "alice@example.com"),
                new KeyValuePair<string, string>("enquiryType", "sales"),
                new KeyValuePair<string, string>("interests", "product"),
                new KeyValuePair<string, string>("sneaky_field", "should be rejected"),
            ]);

            var response = await this.client.PostAsync("/forms/registration/submissions", content, TestContext.Current.CancellationToken);

            await this.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }

        private async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
        {
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(expected, body);
        }

        private StringContent Json(string json) =>
            new StringContent(json, Encoding.UTF8, "application/json");
    }
}
