using FormHandler.Api.Domain;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FormHandler.Api.Infrastructure.Forms
{
    public sealed class YamlFormDefinitionLoader
    {
        private readonly IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeDiscriminatingNodeDeserializer(options =>
            {
                options.AddKeyValueTypeDiscriminator<FieldDescriptor>("type", new Dictionary<string, Type>
                {
                    ["text"] = typeof(TextFieldDescriptor),
                    ["textArea"] = typeof(TextAreaFieldDescriptor),
                    ["email"] = typeof(EmailFieldDescriptor),
                    ["url"] = typeof(UrlFieldDescriptor),
                    ["checkBox"] = typeof(CheckboxFieldDescriptor),
                    ["number"] = typeof(NumberFieldDescriptor),
                    ["slider"] = typeof(SliderFieldDescriptor),
                    ["select"] = typeof(SelectFieldDescriptor),
                    ["radio"] = typeof(RadioGroupDescriptor),
                    ["checkBoxGroup"] = typeof(CheckboxGroupDescriptor)
                });
            })
            .IgnoreUnmatchedProperties()
            .Build();

        public IReadOnlyList<FormDefinition> Load(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new InvalidOperationException($"Forms directory '{directory}' does not exist.");
            }

            var files = Directory.GetFiles(directory, "*.yaml");

            if (files.Length == 0)
            {
                return [];
            }

            var errors = new List<FormLoadError>();
            var forms = new List<FormDefinition>();
            var parsedDescriptors = new List<(string FilePath, FormDescriptor Descriptor)>();

            // Deserialize each YAML file into a descriptor object
            foreach (var filePath in files)
            {
                try
                {
                    var yaml = File.ReadAllText(filePath);
                    var descriptor = this.deserializer.Deserialize<FormDescriptor>(yaml);

                    if (descriptor is null)
                    {
                        errors.Add(new FormLoadError(filePath, FormLoadErrorKind.DeserializationFailure, "File is empty."));
                    }
                    else
                    {
                        parsedDescriptors.Add((filePath, descriptor));
                    }
                }
                catch (YamlException exception)
                {
                    errors.Add(new FormLoadError(filePath, FormLoadErrorKind.DeserializationFailure, exception.Message));
                }
            }

            // Map each descriptor to a domain object
            foreach (var (filePath, descriptor) in parsedDescriptors)
            {
                var (form, mappingErrors) = FormDescriptorMapper.ToFormDefinition(descriptor);

                if (mappingErrors.Count > 0)
                {
                    foreach (var mappingError in mappingErrors)
                    {
                        errors.Add(new FormLoadError(filePath, FormLoadErrorKind.MappingFailure, mappingError));
                    }
                }
                else if (form is not null)
                {
                    forms.Add(form);
                }
            }

            // Check for duplicate form IDs across files
            foreach (var duplicateGroup in forms.GroupBy(form => form.Id).Where(group => group.Count() > 1))
            {
                foreach (var pair in parsedDescriptors.Where(pair => pair.Descriptor.Id == duplicateGroup.Key))
                {
                    errors.Add(new FormLoadError(pair.FilePath, FormLoadErrorKind.DuplicateId,
                        $"Duplicate form ID \"{duplicateGroup.Key}\"."));
                }
            }

            if (errors.Count > 0)
            {
                throw new FormLoadException(errors);
            }

            return forms;
        }
    }
}
