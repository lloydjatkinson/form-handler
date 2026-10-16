using System.Text;

namespace FormHandler.Api.Infrastructure.Forms
{
    public sealed class FormLoadException : Exception
    {
        public IReadOnlyList<FormLoadError> Errors { get; }

        public FormLoadException(IReadOnlyList<FormLoadError> errors)
            : base(BuildMessage(errors))
        {
            this.Errors = errors;
        }

        private static string BuildMessage(IReadOnlyList<FormLoadError> errors)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Form loading failed with {errors.Count} error(s):");

            foreach (var error in errors)
            {
                builder.AppendLine($"  [{error.Kind}] {error.FilePath}: {error.Message}");
            }

            return builder.ToString().TrimEnd();
        }
    }
}
