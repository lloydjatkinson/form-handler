namespace FormHandler.Api.Application.Submissions
{
    public record FormSubmissionMetadata(
        string? IpAddress,
        string? ForwardedFor,
        string? UserAgent,
        string? AcceptLanguage,
        string? Referer,
        string? SecChUa,
        string? SecChUaPlatform,
        string? SecChUaMobile);
}