using System.Text.Json;
using FormHandler.Api.Application;
using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Domain.Submissions;
using FormHandler.Api.Endpoints.Responses;

namespace FormHandler.Api.Endpoints
{
    public static class FormEndpoints
    {
        public static IEndpointRouteBuilder MapForms(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/forms");

            group.MapGet("/{formId}", GetForm)
                .WithName("GetForm")
                .WithSummary("Get a form definition")
                .WithDescription("Returns the field definitions for the specified form.")
                .WithTags("Forms");

            group.MapPost("/{formId}/submissions", SubmitFormUrlEncoded)
                .WithName("SubmitForm")
                .WithSummary("Submit a form")
                .WithDescription("Submit field values for the specified form.")
                .WithTags("Forms")
                .Accepts<IFormCollection>("application/x-www-form-urlencoded")
                .DisableAntiforgery();

            group.MapPost("/{formId}/submissions", SubmitFormJson)
                .WithSummary("Submit a form")
                .WithDescription("Submit field values for the specified form.")
                .WithTags("Forms")
                .Accepts<Dictionary<string, JsonElement>>("application/json");

            return app;
        }

        private static IResult GetForm(IFormRegistry registry, string formId)
        {
            var form = registry.Find(formId);

            return form is null
                ? Results.NotFound()
                : Results.Ok(FormDefinitionMapper.ToResponse(form));
        }

        private static async Task<IResult> SubmitFormUrlEncoded(
            HttpContext httpContext,
            IFormRegistry registry,
            FormSubmissionValidator validator,
            IFormSubmissionStore store,
            IFormCollection form,
            string formId,
            CancellationToken cancellationToken)
        {
            var formDefinition = registry.Find(formId);

            if (formDefinition is null)
            {
                return Results.NotFound();
            }

            var values = FieldValueMapper.FromFormEncoded(formDefinition, form);

            return await HandleSubmission(httpContext, registry, validator, store, values, formId, cancellationToken);
        }

        private static async Task<IResult> SubmitFormJson(
            HttpContext httpContext,
            IFormRegistry registry,
            FormSubmissionValidator validator,
            IFormSubmissionStore store,
            Dictionary<string, JsonElement>? body,
            string formId,
            CancellationToken cancellationToken)
        {
            var formDefinition = registry.Find(formId);

            if (formDefinition is null)
            {
                return Results.NotFound();
            }

            var values = FieldValueMapper.FromJson(formDefinition, body ?? []);

            return await HandleSubmission(httpContext, registry, validator, store, values, formId, cancellationToken);
        }

        private static async Task<IResult> HandleSubmission(
            HttpContext httpContext,
            IFormRegistry registry,
            FormSubmissionValidator validator,
            IFormSubmissionStore store,
            IReadOnlyDictionary<string, FieldValue> values,
            string formId,
            CancellationToken cancellationToken)
        {
            var formDefinition = registry.Find(formId);

            if (formDefinition is null)
            {
                return Results.NotFound();
            }

            var submission = new FormSubmission() { FormId = formId, Values = values };
            var result = validator.Validate(formDefinition, submission);

            if (result is FormSubmissionResult.Invalid invalid)
            {
                var errors = invalid.Errors
                    .GroupBy(error => error.FieldName)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray());

                return Results.ValidationProblem(errors);
            }

            await store.SaveAsync(submission, httpContext.GetMetadata(), cancellationToken);

            return Results.Created();
        }
    }
}
