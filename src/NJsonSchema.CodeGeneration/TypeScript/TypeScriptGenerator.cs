﻿//-----------------------------------------------------------------------
// <copyright file="CSharpClassGenerator.cs" company="NJsonSchema">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/rsuter/NJsonSchema/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace NJsonSchema.CodeGeneration.TypeScript
{
    /// <summary>The TypeScript interface and enum code generator. </summary>
    public class TypeScriptGenerator : TypeGeneratorBase
    {
        private readonly JsonSchema4 _schema;
        private readonly TypeScriptTypeResolver _resolver;

        /// <summary>Initializes a new instance of the <see cref="TypeScriptGenerator"/> class.</summary>
        /// <param name="schema">The schema.</param>
        public TypeScriptGenerator(JsonSchema4 schema)
            : this(schema, new TypeScriptGeneratorSettings())
        {
        }

        /// <summary>Initializes a new instance of the <see cref="TypeScriptGenerator"/> class.</summary>
        /// <param name="settings">The generator settings.</param>
        /// <param name="schema">The schema.</param>
        public TypeScriptGenerator(JsonSchema4 schema, TypeScriptGeneratorSettings settings)
            : this(schema, settings, new TypeScriptTypeResolver(settings))
        {
        }

        /// <summary>Initializes a new instance of the <see cref="TypeScriptGenerator"/> class.</summary>
        /// <param name="schema">The schema.</param>
        /// <param name="settings">The generator settings.</param>
        /// <param name="resolver">The resolver.</param>
        public TypeScriptGenerator(JsonSchema4 schema, TypeScriptGeneratorSettings settings, TypeScriptTypeResolver resolver)
        {
            _schema = schema;
            _resolver = resolver;
            Settings = settings;
        }

        /// <summary>Gets the generator settings.</summary>
        public TypeScriptGeneratorSettings Settings { get; set; }

        /// <summary>Gets the language.</summary>
        protected override string Language => "TypeScript";

        /// <summary>Generates the file.</summary>
        /// <returns>The file contents.</returns>
        public override string GenerateFile()
        {
            return GenerateType(string.Empty).Code + "\n\n" + _resolver.GenerateTypes();
        }

        /// <summary>Generates the type.</summary>
        /// <param name="typeNameHint">The type name hint.</param>
        /// <returns>The code.</returns>
        public override TypeGeneratorResult GenerateType(string typeNameHint)
        {
            var typeName = !string.IsNullOrEmpty(_schema.TypeName) ? _schema.TypeName : _resolver.GenerateTypeName(typeNameHint);

            if (_schema.IsEnumeration)
            {
                var template = LoadTemplate("Enum");

                if (_schema.Type == JsonObjectType.Integer)
                    typeName = typeName + "AsInteger";

                template.Add("name", typeName);
                template.Add("enums", GetEnumeration());

                template.Add("hasDescription", !(_schema is JsonProperty) && !string.IsNullOrEmpty(_schema.Description));
                template.Add("description", RemoveLineBreaks(_schema.Description));

                return new TypeGeneratorResult
                {
                    TypeName = typeName,
                    Code = template.Render()
                };
            }
            else
            {
                var properties = _schema.Properties.Values.Select(property => new
                {
                    Name = property.Name,
                    InterfaceName = property.Name.Contains("-") ? '\"' + property.Name + '\"' : property.Name,
                    PropertyName = ConvertToLowerCamelCase(property.Name).Replace("-", "_"),

                    Type = _resolver.Resolve(property, property.Type.HasFlag(JsonObjectType.Null), property.Name),

                    IsNewableObject = IsNewableObject(property),
                    IsDate = property.ActualSchema.Format == JsonFormatStrings.DateTime,

                    IsDictionary = property.ActualSchema.IsDictionary,
                    DictionaryValueType = TryResolve(property.ActualSchema.AdditionalPropertiesSchema, property.Name),
                    IsDictionaryValueNewableObject = property.ActualSchema.AdditionalPropertiesSchema != null && IsNewableObject(property.ActualSchema.AdditionalPropertiesSchema),
                    IsDictionaryValueDate = property.ActualSchema.AdditionalPropertiesSchema?.Format == JsonFormatStrings.DateTime,

                    IsArray = property.ActualSchema.Type.HasFlag(JsonObjectType.Array),
                    ArrayItemType = TryResolve(property.ActualSchema.Item, property.Name),
                    IsArrayItemNewableObject = property.ActualSchema.Item != null && IsNewableObject(property.ActualSchema.Item), 
                    IsArrayItemDate = property.ActualSchema.Item?.Format == JsonFormatStrings.DateTime, 

                    Description = property.Description,
                    HasDescription = !string.IsNullOrEmpty(property.Description),

                    IsReadOnly = property.IsReadOnly && Settings.GenerateReadOnlyKeywords,
                    IsOptional = !property.IsRequired
                }).ToList();

                var template = LoadTemplate(Settings.TypeStyle.ToString());
                template.Add("class", typeName);

                template.Add("hasDescription", !(_schema is JsonProperty) && !string.IsNullOrEmpty(_schema.Description));
                template.Add("description", RemoveLineBreaks(_schema.Description));

                var hasInheritance = _schema.AllOf.Count == 1;
                template.Add("hasInheritance", hasInheritance);
                template.Add("inheritance", hasInheritance ? " extends " + _resolver.Resolve(_schema.AllOf.First(), true, string.Empty) : string.Empty);
                template.Add("properties", properties);

                return new TypeGeneratorResult
                {
                    TypeName = typeName,
                    Code = template.Render()
                };
            }
        }

        private string TryResolve(JsonSchema4 schema, string typeNameHint)
        {
            return schema != null ? _resolver.Resolve(schema, false, typeNameHint) : string.Empty;
        }

        private static bool IsNewableObject(JsonSchema4 schema)
        {
            schema = schema.ActualSchema;
            return schema.Type.HasFlag(JsonObjectType.Object) && !schema.IsAnyType && !schema.IsDictionary;
        }

        private List<EnumerationEntry> GetEnumeration()
        {
            var entries = new List<EnumerationEntry>();
            for (int i = 0; i < _schema.Enumeration.Count; i++)
            {
                var value = _schema.Enumeration.ElementAt(i);
                var name = _schema.EnumerationNames.Count > i ?
                    _schema.EnumerationNames.ElementAt(i) :
                    _schema.Type == JsonObjectType.Integer ? "Value" + value : value.ToString();

                entries.Add(new EnumerationEntry
                {
                    Value = _schema.Type == JsonObjectType.Integer ? value.ToString() : "<any>\"" + value + "\"",
                    Name = ConvertToUpperCamelCase(name)
                });
            }
            return entries;
        }
    }
}
