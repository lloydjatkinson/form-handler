using System.Text.Json;
using FormHandler.Api.Application.Forms;
using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Domain;
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

        private static IResult GetForm(IFormDefinitionStore store, string formId)
        {
            var form = store.FindById(formId);

            return form is null
                ? Results.NotFound()
                : Results.Ok(FormDefinitionMapper.ToResponse(form));
        }

        private static async Task<IResult> SubmitFormUrlEncoded(
            HttpContext httpContext,
            IFormDefinitionStore store,
            FormSubmissionValidator validator,
            IFormSubmissionStore submissionStore,
            IFormCollection form,
            string formId,
            CancellationToken cancellationToken)
        {
            var formDefinition = store.FindById(formId);

            if (formDefinition is null)
            {
                return Results.NotFound();
            }

            var values = FieldValueMapper.FromFormEncoded(formDefinition, form);

            return await HandleSubmission(httpContext, formDefinition, validator, submissionStore, values, cancellationToken);
        }

        private static async Task<IResult> SubmitFormJson(
            HttpContext httpContext,
            IFormDefinitionStore store,
            FormSubmissionValidator validator,
            IFormSubmissionStore submissionStore,
            Dictionary<string, JsonElement>? body,
            string formId,
            CancellationToken cancellationToken)
        {
            var formDefinition = store.FindById(formId);

            if (formDefinition is null)
            {
                return Results.NotFound();
            }

            var values = FieldValueMapper.FromJson(formDefinition, body ?? []);

            return await HandleSubmission(httpContext, formDefinition, validator, submissionStore, values, cancellationToken);
        }

        private static async Task<IResult> HandleSubmission(
            HttpContext httpContext,
            FormDefinition formDefinition,
            FormSubmissionValidator validator,
            IFormSubmissionStore submissionStore,
            IReadOnlyDictionary<string, FieldValue> values,
            CancellationToken cancellationToken)
        {
            var submission = new FormSubmission { FormId = formDefinition.Id, Values = values };
            var result = validator.Validate(formDefinition, submission);

            if (result is FormSubmissionResult.Invalid invalid)
            {
                var errors = invalid.Errors
                    .GroupBy(error => error.FieldName)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray());

                return Results.ValidationProblem(errors);
            }

            await submissionStore.SaveAsync(submission, httpContext.GetMetadata(), cancellationToken);

            return Results.Created();
        }
    }
}
