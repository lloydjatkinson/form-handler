using System.Collections.Frozen;
using FormHandler.Api.Application.Forms;
using FormHandler.Api.Domain;

namespace FormHandler.Api.Infrastructure.Forms
{
    public sealed class InMemoryFormDefinitionStore : IFormDefinitionStore
    {
        private readonly FrozenDictionary<string, FormDefinition> forms;

        public InMemoryFormDefinitionStore(IReadOnlyList<FormDefinition> forms)
        {
            this.forms = forms.ToFrozenDictionary(form => form.Id);
        }

        public IReadOnlyList<FormDefinition> GetAll() => [.. this.forms.Values];

        public FormDefinition? FindById(string id) => this.forms.GetValueOrDefault(id);
    }
}
