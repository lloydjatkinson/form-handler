namespace FormHandler.Api.Domain.Submissions
{
    public abstract class FormSubmissionResult
    {
        public sealed class Success : FormSubmissionResult
        { }

        public sealed class Invalid : FormSubmissionResult
        {
            public Invalid(IReadOnlyList<FieldValidationError> errors)
            {
                this.Errors = errors;
            }

            public IReadOnlyList<FieldValidationError> Errors { get; }
        }
    }
}