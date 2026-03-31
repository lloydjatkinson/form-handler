using System.Collections.Frozen;
using System.Collections.Generic;
using FormHandler.Api.Application;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;

namespace FormHandler.Tests.Endpoints
{
    internal sealed class SurveyFormRegistry : IFormRegistry
    {
        private readonly FrozenDictionary<string, FormDefinition> forms = new[]
        {
            new FormDefinition
            {
                EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com" },                Id = "survey",
                Name = "Market Listing",
                Fields =
                [
                    new TextFieldDefinition
                    {
                        Id = "name",
                        Name = "name",
                        Label = "Full Name",
                        IsRequired = true,
                        MaxLength = 200
                    },
                    new TextFieldDefinition
                    {
                        Id = "stall",
                        Name = "stall",
                        Label = "Stall Number"
                    },
                    new EmailFieldDefinition
                    {
                        Id = "email",
                        Name = "email",
                        Label = "Email Address",
                        IsRequired = true
                    },
                    new UrlFieldDefinition
                    {
                        Id = "website",
                        Name = "website",
                        Label = "Website"
                    },
                    new RadioGroupDefinition
                    {
                        Id = "type",
                        Name = "type",
                        Label = "Are you a buyer or seller?",
                        IsRequired = true,
                        Options =
                        [
                            new FieldOption("buyer", "Buyer"),
                            new FieldOption("seller", "Seller"),
                            new FieldOption("both", "Both")
                        ]
                    },
                    new CheckboxGroupDefinition
                    {
                        Id = "produce",
                        Name = "produce",
                        Label = "Produce of Interest",
                        IsRequired = true,
                        Options =
                        [
                            new FieldOption("apples", "Apples"),
                            new FieldOption("oranges", "Oranges"),
                            new FieldOption("bananas", "Bananas"),
                            new FieldOption("grapes", "Grapes")
                        ]
                    },
                    new SelectFieldDefinition
                    {
                        Id = "preferred-day",
                        Name = "preferredDay",
                        Label = "Preferred Market Day",
                        Options =
                        [
                            new FieldOption("sat", "Saturday"),
                            new FieldOption("sun", "Sunday")
                        ]
                    },
                    new CheckboxGroupDefinition
                    {
                        Id = "days",
                        Name = "days",
                        Label = "Days Available",
                        Options =
                        [
                            new FieldOption("mon", "Monday"),
                            new FieldOption("wed", "Wednesday"),
                            new FieldOption("fri", "Friday"),
                            new FieldOption("sat", "Saturday"),
                            new FieldOption("sun", "Sunday")
                        ]
                    },
                    new NumberFieldDefinition
                    {
                        Id = "quantity",
                        Name = "quantity",
                        Label = "Estimated Weekly Quantity (kg)",
                        Min = 0,
                        Max = 10000
                    },
                    new SliderFieldDefinition
                    {
                        Id = "rating",
                        Name = "rating",
                        Label = "Quality Rating",
                        Min = 1,
                        Max = 10,
                        Step = 1
                    },
                    new TextAreaFieldDefinition
                    {
                        Id = "notes",
                        Name = "notes",
                        Label = "Additional Notes"
                    },
                    new CheckboxFieldDefinition
                    {
                        Id = "newsletter",
                        Name = "newsletter",
                        Label = "Subscribe to market newsletter"
                    }
                ]
            }
        }.ToFrozenDictionary(form => form.Id);

        public FormDefinition? Find(string id) => this.forms.GetValueOrDefault(id);
    }
}