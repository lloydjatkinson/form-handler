using System;
using System.Collections.Generic;
using AutoFixture.Kernel;
using FormHandler.Api.Domain;

namespace FormHandler.Tests
{
    public sealed class FieldDefinitionListSpecimenBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (request is not Type type || type != typeof(IReadOnlyList<FieldDefinition>))
            {
                return new NoSpecimen();
            }

            return Array.Empty<FieldDefinition>();
        }
    }
}
