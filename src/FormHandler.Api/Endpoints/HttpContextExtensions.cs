using FormHandler.Api.Application.Submissions;

namespace FormHandler.Api.Endpoints
{
    public static class HttpContextExtensions
    {
        public static FormSubmissionMetadata GetMetadata(this HttpContext context) =>
            new FormSubmissionMetadata(
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers["X-Forwarded-For"].ToString(),
                context.Request.Headers.UserAgent.ToString(),
                context.Request.Headers.AcceptLanguage.ToString(),
                context.Request.Headers.Referer.ToString(),
                context.Request.Headers["Sec-CH-UA"].ToString(),
                context.Request.Headers["Sec-CH-UA-Platform"].ToString(),
                context.Request.Headers["Sec-CH-UA-Mobile"].ToString());
    }
}