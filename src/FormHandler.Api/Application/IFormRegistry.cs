using FormHandler.Api.Domain;

namespace FormHandler.Api.Application
{
    public interface IFormRegistry
    {
        FormDefinition? Find(string id);
    }
}