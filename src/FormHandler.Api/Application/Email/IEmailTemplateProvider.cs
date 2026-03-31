namespace FormHandler.Api.Application.Email
{
    public interface IEmailTemplateProvider
    {
        string GetTemplate(string name);
    }
}