# ErrorProne.Net.Annotations

Build-time source generation for ErrorProne.NET attributes, with **no additional
runtime DLL**. The generator embeds internal attribute types directly into each
project that references this package.

```xml
<PackageReference Include="ErrorProne.Net.Annotations"
                  Version="YOUR_VERSION"
                  IncludeAssets="analyzers;build;buildtransitive"
                  PrivateAssets="all" />
```

Replace `YOUR_VERSION` with the package version you consume. The generator
requires a Roslyn 4.13 or newer C# compiler. Its generated source supports C# 7.3
and newer. Reference the analyzer package separately to enable diagnostics.

```csharp
// With RootNamespace = Azure.Core:
namespace Azure.Core
{
    public interface IResources
    {
        [return: ReturnsOwnership]
        System.IDisposable Open();

        [return: DoNotDispose]
        System.IDisposable GetShared();
    }
}
```

## Namespace configuration

Types are generated directly in the consuming project's `RootNamespace`, not
necessarily its assembly name. Nested namespaces can use the attributes without
adding another namespace import. To override only the annotation namespace:

```xml
<PropertyGroup>
  <ErrorProneAnnotationsNamespace>Azure.Core.InternalAnnotations</ErrorProneAnnotationsNamespace>
</PropertyGroup>
```

An empty override uses `RootNamespace`. An explicitly empty `RootNamespace`
generates types in the global namespace. Invalid namespaces produce EPANN002;
missing namespace build configuration produces EPANN001. Include the package's
build assets so these properties reach the compiler.

## Public contracts without public attribute types

Internal attributes can annotate public APIs. Their usages remain in both the
compiled library and its reference assembly. A downstream Roslyn analyzer reads
these contracts from metadata; consumer code does not need access to the
attribute class or a runtime reference to the generator.

The attributes are not conditional and are not stripped from metadata.
Each consuming project that wants to annotate its own code references this
package directly. `PrivateAssets="all"` prevents the generator package from
becoming a transitive dependency of your library.

No public, shared runtime identity for these attribute types is promised.
Consumers should not depend on using `typeof` or generic reflection APIs with
another assembly's internal attribute types. Metadata-based analysis works
without such access.

## Generated attributes

The generator provides:

- `AcquiresOwnership`: consuming parameters.
- `ReturnsOwnership`: owning method and property results.
- `DoNotDispose`: borrowed parameters, fields, properties, and results.
- `KeepsOwnership` and `NoOwnership`: compatibility ownership names.
- `MustUseResult`: method results that must be observed.
- `MustUseReturnValue`: a recognized compatibility name for `MustUseResult`.
- `UseConfigureAwaitFalse`: assembly-wide policy requiring ConfigureAwait.
- `DoNotUseConfigureAwait`: assembly-wide policy marking `ConfigureAwait(false)` as redundant.

### Non-ownership annotations

These annotations work independently of disposable ownership:

```csharp
// With RootNamespace = MyProject, choose the assembly policy appropriate for the project:
[assembly: MyProject.UseConfigureAwaitFalse]
// Alternatively: [assembly: MyProject.DoNotUseConfigureAwait]

namespace MyProject
{
    public static class Validation
    {
        [MustUseResult]
        public static bool IsValid(string value) => !string.IsNullOrEmpty(value);
    }
}
```

Ignoring an annotated method's result reports [EPC34](../../docs/Rules/EPC34.md),
including an ignored awaited result. `MustUseResult` is the preferred name;
`MustUseReturnValue` has the same analyzer behavior. Assembly-level policies
enable [EPC15](../../docs/Rules/EPC15.md) or [EPC14](../../docs/Rules/EPC14.md),
respectively. Generating the attribute types alone does not select a policy:
apply the assembly attribute explicitly.

All of these types are internal and source-embedded, just like the ownership
attributes. They add no runtime DLL dependency.

## Existing definitions

If an accessible attribute type with the same fully qualified name already
exists, its definition is used instead of generating a duplicate. Inaccessible
internal definitions in other assemblies do not prevent generating a local copy,
and referenced non-attribute types with the same name do not suppress local
generation.
For the ConfigureAwait assembly policies, recognized legacy attribute classes
named `UseConfigureAwaitFalse` or `DoNotUseConfigureAwait` without the `Attribute`
suffix are also reused to avoid ambiguous attribute names.
Definitions reachable only through an `extern alias` also do not prevent local
generation, since the generated source cannot use those types by their namespace.
Friend/test assemblies can omit the generator when `InternalsVisibleTo` already
makes the required annotations available.

If multiple referenced definitions are accessible and ambiguous, EPANN003
requests a namespace override or removal of the conflicting reference. The
generator does not guess which assembly to reuse, silently change namespace,
or switch to public attribute types. If the current assembly already declares a
same-name non-attribute type in the target namespace, EPANN004 reports that
local conflict instead of generating a duplicate attribute declaration.

This package does not add type-level ownership markers or generate BCL
nullability attributes. Existing application-defined and legacy ErrorProne.NET
attribute names remain recognized by the analyzers.
