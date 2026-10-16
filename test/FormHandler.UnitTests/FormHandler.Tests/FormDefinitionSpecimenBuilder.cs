using System;
using AutoFixture.Kernel;
using FormHandler.Api.Domain;

namespace FormHandler.Tests
{
    // FormDefinition has no public constructor (it is built through FormDefinition.Create),
    // so teach AutoFixture to produce a valid one via the factory.
    public sealed class FormDefinitionSpecimenBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (request is not Type type || type != typeof(FormDefinition))
            {
                return new NoSpecimen();
            }

            var id = (string)context.Resolve(typeof(string));
            var name = (string)context.Resolve(typeof(string));
            var emailOptions = new FormSubmissionEmailOptions { To = TestEmail.Email("auto@example.com") };

            return FormDefinition.Create(id, name, emailOptions, []).Form!;
        }
    }
}
