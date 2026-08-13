using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// An analyzer that warns when a data member of a type marked with 'DataContractAttribute'
    /// has a type that cannot be serialized by 'DataContractSerializer'.
    /// </summary>
    /// <remarks>
    /// 'DataContractSerializer' validates the object graph lazily, i.e. the constructor succeeds and
    /// the failure happens on the first serialization attempt with 'InvalidDataContractException'.
    /// A canonical example is 'System.Net.IPAddress' that is serializable on the .NET Framework but is
    /// not serializable on .NET Core, because it is not marked with 'SerializableAttribute' and has no
    /// parameterless constructor.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DataContractSerializableMemberAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC42;

        /// <nodoc />
        public DataContractSerializableMemberAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(compilationContext =>
            {
                var knownTypes = SerializationTypes.TryCreate(compilationContext.Compilation);
                if (knownTypes is null)
                {
                    // 'System.Runtime.Serialization' is not referenced, nothing to do.
                    return;
                }

                compilationContext.RegisterSymbolAction(symbolContext => AnalyzeNamedType(symbolContext, knownTypes), SymbolKind.NamedType);
            });
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context, SerializationTypes knownTypes)
        {
            var type = (INamedTypeSymbol)context.Symbol;

            // Note that this check covers more than plain classes and structs:
            // a 'record' is a class ('TypeKind.Class') and a 'record struct' is a struct ('TypeKind.Struct'),
            // so all the record flavors are analyzed here as well.
            // The same is true for the union types: a union is compiled into a struct.
            // Everything else (interfaces, enums, delegates) cannot be a data contract.
            if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
            {
                return;
            }

            if (!type.HasAttribute(knownTypes.DataContractAttribute))
            {
                return;
            }

            foreach (var member in type.GetMembers())
            {
                if (member.IsStatic || member.IsImplicitlyDeclared)
                {
                    continue;
                }

                ITypeSymbol memberType;
                switch (member)
                {
                    case IPropertySymbol property when !property.IsIndexer:
                        memberType = property.Type;
                        break;
                    case IFieldSymbol field when !field.IsConst:
                        memberType = field.Type;
                        break;
                    default:
                        continue;
                }

                if (!member.HasAttribute(knownTypes.DataMemberAttribute))
                {
                    // Only the members marked with 'DataMemberAttribute' are serialized
                    // when the enclosing type is marked with 'DataContractAttribute'.
                    continue;
                }

                foreach (var candidate in EnumerateTypesToCheck(memberType))
                {
                    if (!IsDataContractSerializable(candidate, knownTypes, out var reason))
                    {
                        var location = member.Locations.FirstOrDefault() ?? Location.None;
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Rule,
                                location,
                                $"{type.Name}.{member.Name}",
                                reason));

                        // Reporting a single diagnostic per member.
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Returns all the types that have to be serializable in order for <paramref name="type"/> to be serializable.
        /// </summary>
        /// <remarks>
        /// Arrays and 'Nullable{T}' are unwrapped and the generic arguments are checked as well,
        /// because 'List{IPAddress}' or 'IPAddress[]' fail exactly the same way a plain 'IPAddress' member does.
        /// </remarks>
        private static IEnumerable<ITypeSymbol> EnumerateTypesToCheck(ITypeSymbol type)
        {
            // The recursion always terminates, because every step strips one layer off the type.
            switch (type)
            {
                case IArrayTypeSymbol arrayType:
                    return EnumerateTypesToCheck(arrayType.ElementType);
                case INamedTypeSymbol nullableType when nullableType.IsNullableType() && nullableType.TypeArguments.Length == 1:
                    return EnumerateTypesToCheck(nullableType.TypeArguments[0]);
                case INamedTypeSymbol genericType when genericType.IsGenericType:
                    return new[] { (ITypeSymbol)genericType }.Concat(genericType.TypeArguments.SelectMany(EnumerateTypesToCheck));
                default:
                    return new[] { type };
            }
        }

        private static bool IsDataContractSerializable(ITypeSymbol type, SerializationTypes knownTypes, out string reason)
        {
            reason = string.Empty;

            switch (type.TypeKind)
            {
                // Note that the type parameters, pointers, function pointers and 'dynamic' are not
                // 'INamedTypeSymbol' and are filtered out by the check below.
                case TypeKind.Error:
                case TypeKind.Enum:
                // Delegates are not supported by the serializer, but they're typically used
                // in a combination with 'IgnoreDataMemberAttribute' and are out of scope of this rule.
                case TypeKind.Delegate:
                // The runtime type of an interface-typed member is unknown at compile time.
                // Such cases fail with a different exception and are solved with 'KnownTypeAttribute'.
                case TypeKind.Interface:
                    return true;
            }

            if (type.SpecialType != SpecialType.None)
            {
                // Primitives, 'string', 'object', 'decimal', 'DateTime' etc.
                return true;
            }

            if (type is not INamedTypeSymbol namedType)
            {
                return true;
            }

            if (namedType.IsAbstract)
            {
                // The runtime type is unknown at compile time. See the comment for the interfaces.
                return true;
            }

            if (namedType.HasAttribute(knownTypes.DataContractAttribute) ||
                namedType.HasAttribute(knownTypes.CollectionDataContractAttribute) ||
                // 'SerializableAttribute' is a metadata flag and is not exposed via 'GetAttributes'
                // for the types coming from metadata.
                namedType.IsSerializable)
            {
                return true;
            }

            if (namedType.ImplementsAny(knownTypes.ISerializable, knownTypes.IXmlSerializable))
            {
                return true;
            }

            // Falling back to the 'POCO' data contract: the type must be public
            // and must have a parameterless constructor (a non-public one is fine).
            var isPublic = IsPubliclyVisible(namedType);

            // Value types always have a parameterless constructor.
            var hasParameterlessConstructor =
                namedType.IsValueType || namedType.InstanceConstructors.Any(c => c.Parameters.Length == 0);

            if (isPublic && hasParameterlessConstructor)
            {
                return true;
            }

            var typeName = namedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            var problem = (isPublic, hasParameterlessConstructor) switch
            {
                (false, false) => "is not public and has no parameterless constructor",
                (false, true) => "is not public",
                _ => "has no parameterless constructor",
            };

            reason = $"type '{typeName}' {problem} and is not marked with [DataContract], [CollectionDataContract] or [Serializable]";
            return false;
        }

        private static bool IsPubliclyVisible(INamedTypeSymbol type)
        {
            for (var current = type; current is not null; current = current.ContainingType)
            {
                if (current.DeclaredAccessibility != Accessibility.Public &&
                    current.DeclaredAccessibility != Accessibility.NotApplicable)
                {
                    return false;
                }
            }

            return true;
        }

        private sealed record SerializationTypes(
            INamedTypeSymbol DataContractAttribute,
            INamedTypeSymbol DataMemberAttribute,
            INamedTypeSymbol? CollectionDataContractAttribute,
            INamedTypeSymbol? ISerializable,
            INamedTypeSymbol? IXmlSerializable)
        {
            public static SerializationTypes? TryCreate(Compilation compilation)
            {
                var provider = WellKnownTypeProvider.GetOrCreate(compilation);
                var dataContractAttribute = provider.GetTypeByFullName("System.Runtime.Serialization.DataContractAttribute");
                var dataMemberAttribute = provider.GetTypeByFullName("System.Runtime.Serialization.DataMemberAttribute");

                if (dataContractAttribute is null || dataMemberAttribute is null)
                {
                    return null;
                }

                return new SerializationTypes(
                    dataContractAttribute,
                    dataMemberAttribute,
                    provider.GetTypeByFullName("System.Runtime.Serialization.CollectionDataContractAttribute"),
                    provider.GetTypeByFullName("System.Runtime.Serialization.ISerializable"),
                    provider.GetTypeByFullName("System.Xml.Serialization.IXmlSerializable"));
            }
        }
    }
}
