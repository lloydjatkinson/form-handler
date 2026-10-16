using System;
using AutoFixture;
using AutoFixture.Kernel;
using FormHandler.Api.Domain.Submissions;

namespace FormHandler.Tests
{
    public sealed class FieldValueSpecimenBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (request is not Type type || type != typeof(FieldValue))
            {
                return new NoSpecimen();
            }

            var types = new Func<FieldValue>[]
            {
                () => new FieldValue.Text(context.Create<string>()),
                () => new FieldValue.Number(context.Create<decimal>()),
                () => new FieldValue.Toggle(context.Create<bool>()),
                () => new FieldValue.Selection([context.Create<string>()])
            };

            return types[context.Create<int>() % types.Length]();
        }
    }
}