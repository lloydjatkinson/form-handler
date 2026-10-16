using FormHandler.Api.Domain;

namespace FormHandler.Api.Application.Forms
{
    public interface IFormDefinitionStore
    {
        IReadOnlyList<FormDefinition> GetAll();
        FormDefinition? FindById(string id);
    }
}
