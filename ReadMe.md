# Error Prone .NET

[![Build Status](https://seteplia.visualstudio.com/ErrorProne.NET/_apis/build/status/SergeyTeplyakov.ErrorProne.NET?label=build)](https://seteplia.visualstudio.com/ErrorProne.NET/_build/latest?definitionId=1)

ErrorProne.NET is a set of Roslyn-based analyzers that will help you to write correct code. The idea is similar to Google's [error-prone](https://github.com/google/error-prone) but instead of Java, the analyzers are focusing on correctness (and, maybe, performance) of C# programs.

Currently, there are two types of analyzers that split into two projects that ended up in two separate nuget packages: 
* ErrorProne.CoreAnalyzers - the analyzers covering the most common cases that may occur in almost any project, like error handling or correctness of some widely used API.
* ErrorProne.StructAnalyzers - the analyzers focusing on potential performance problem when dealing with structs in C#.

## ErrorProne.CoreAnalyzers
The "core" analyzers are the analyzers that will be useful in almost any .NET project by focusing on the correctness aspects and not on performance or low memory footprint.

### Core Analyzers

#### Unobserved Result Analysis
Some projects are heavily rely on a set of special result type and instead of exception handling patterns are using `Result<T>` or `Possible<T>` families of types.
In this case, it is very important for the callers of operations that return a result to observe it:

```csharp
public class Result
{
    public bool Success = false;
}

public static Result ProcessRequest() => null;

// Result of type 'Result' should be observed
ProcessRequest();
//~~~~~~~~~~~~
```

The analyzers emit a diagnostic for every method that return a type with `Result` in its name as well as for some other well-known types that should be observed in vast majority of cases:
```csharp
Stream s = null;
// The result of type 'Task' should be observed
s.FlushAsync();
//~~~~~~~~~~

// The result of type 'Exception' should be observed
getException();
//~~~~~~~~~~
Exception getException() => null;
```

### Suspisous equality implementation
```csharp
public class FooBar : IEquatable<FooBar>
{
    private string _s;

    // Suspicious equality implementation: parameter 'other' is never used
    public bool Equals(FooBar other)
    //                        ~~~~~
    {
        return _s == "42";
    }
}

public class Baz : IEquatable<Baz>
{
    private string _s;

    // Suspicious equality implementation: no instance members are used
    public bool Equals(Baz other)
    //          ~~~~~~
    {
        return other != null;
    }
}
```

### Exception handling analyzers

Correct exception handling is a very complex topic, but one case that is very common requires special attention. A generic handler that handles `System.Exception` or `System.AggregateException` should "observe" the whole exception instance and not only the `Message` property. The `Message` property is quite important, but in some cases it can be quite meaningless. For instance, the `Message` property for `TargetInvocationException`, `AggregateException`, `TypeLoadExcpetion` and some others doesn't provide anything useful in it's message and the most useful information is stored in `InnerException`.

To avoid this anti-pattern, the analyzer will warn you if the code only traces the `Exception` property without "looking inside":

```csharp
try
{
    Console.WriteLine();
}
catch (Exception e)
{
    // Suspicious exception handling: only e.Message is observed in exception block
    Console.WriteLine(e.Message);
    //                  ~~~~~~~
}

try
{
    Console.WriteLine();
}
catch (Exception e)
{
    // Exit point 'return' swallows an unobserved exception
    return;
//  ~~~~~~
}

try
{
    Console.WriteLine();
}
catch (Exception e)
{
    // Incorrect exception propagation: use 'throw' instead
    throw e;
    //    ~
}
```

### Async Analyzers

```csharp
Stream sample = null;
// Awaiting the result of a null-conditional expression may cause NullReferenceException.
await sample?.FlushAsync();
```

#### Configuring `ConfigureAwait` behavior

The strictness of whether to use `ConfigureAwait` everywhere is very much depends on the project and the layer of the project. It is very important for all the library code to always use `ConfigureAwait(false)` to avoid potential issues like deadlocks. On the other hand, some other parts of the system maybe used only in service layer and the team may decide not to litter the code with redudnat `ConfigureAwait(false)` calls. But regardless of what a team decides to do - whether to call `ConfigureAwait` or not, its very important to enforce (if possible) the consistency of the code.

ErrorProne.NET allows a developer to "annotate" the assembly with an attribute and the anlyzers will enforce the desired behavior:

```csharp
[assembly: UseConfigureAwaitFalse()]
public class UseConfigureAwaitFalseAttribute : System.Attribute { }

public static async Task CopyTo(Stream source, Stream destination)
{
    // ConfigureAwait(false) must be used
    await source.CopyToAsync(destination);
    //    ~~~~~~~~~~~~~~~~~~
}
```

```csharp
[assembly: DoNotUseConfigureAwaitFalse()]
public class UseConfigureAwaitFalseAttribute : System.Attribute { }
// ConfigureAwait(false) call is redundant
await source.CopyToAsync(destination).ConfigureAwait(false);
//                                    ~~~~~~~~~~~~~~~~~~~~~
```

In both cases, the analyzers will look for a special attribute used for a given assembly and will emit diagnostics based on the required usage of `ConfigureAwait`. In the second case, the `ConfigureAwait(false)` will be grayed-out in the IDE and the IDE will suggest a fixer to remove the redundant call.

Please note, that you don't have to reference any ErrorProne.NET assembly in order to use this feature. You can just declare the two attributes by yourself and the analyzer will use duck-typing approach to detect that the right attributes were used.

## Struct Analyzers

Value types are very important in high performance scenarios, but they have its own limitations and hidden aspects that can cause incorrect behavior or performance degradations.

### Do not use default contructors for structs

In high-performant code it is quite common to use structs as an optimization tool. And in some cases, the default constructor for structs can not establish the invariants required for the correct behavior. Such structs can be marked with a special attribute (`DoNotUseDefaultConstruction`) and any attempt to create the struct marked with this attribute using `new` or `default` will trigger a diagnostic:


```csharp
[System.AttributeUsage(System.AttributeTargets.Struct)]
public class DoNotUseDefaultConstructionAttribute : System.Attribute { }

[DoNotUseDefaultConstruction]
public readonly struct TaskSourceSlim<T>
{
    private readonly TaskCompletionSource<T> _tcs;
    public TaskSourceSlim(TaskCompletionSource<T> tcs) => _tcs = tcs;
    // Other members
}

// Do not use default construction for struct 'TaskSourceSlim' marked with 'DoNotUseDefaultConstruction' attribute
var tss = new TaskSourceSlim<object>();
//        ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

// The same warning.
TaskSourceSlim<object> tss2 = default;
//                            ~~~~~~~

// The same warning.
var tss3 = Create<TaskSourceSlim<object>>();
//         ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

var a = new TaskSourceSlim<object>[5]; // OK

// The same warning! ImmutableArray will throw NRE
var ia = new ImmutableArray<int>();
//           ~~~~~~~~~~~~~~~~~~~~~

public static T Create<T>() where T : new() => default;
```

And you can't embed a struct marked with this attribute into another struct, unless the other struct is marked with `DoNotUseDefaultConstruction` attribute as well:

```csharp
public readonly struct S2
{
    // Do not embed struct 'TaskSourceSlim' marked with 'DoNotUseDefaultConstruction' attribute into another struct
    private readonly TaskSourceSlim<object> _tss;
    //               ~~~~~~~~~~~~~~~~~~~~~~~~~~~
}
```

### A hashtable unfriendly type is used as a key in a dictionary

Every type that is used as the key in a dictionary must implement `Equals` and `GetHashCode`. By default the CLR provides the default implementations for `Equals` and `GetHashCode` that follows "value semantics", i.e. the two instances of a struct are equals when all the fields are equals. But unfortunately, the calling a default `Equal` or `GetHashCode` methods causes boxing allocation and may be implemented using reflection, that can be extremely slow.

```csharp
public struct MyStruct
{
    private readonly long _x;
    private readonly long _y;
    public void FooBar() { }
    // Struct 'MyStruct' with the default Equals and HashCode implementation
    // is used as a key in a hash table.
    private static Dictionary<MyStruct, string> Table;
    //             ~~~~~~~~~~~~~~~~~~~~~~~~~~~~
}
```

The same issue may occur when `MyStruct` instance is embedded into another struct that provide custom implementations for `Equals` and `GetHashCode`. And there is an analyzer that warns in this case as well:

```csharp

```

### A struct can be made readonly
Marking a struct readonly can be beneficial in terms of design, because it allows conveying the intent more clearly, and also readonly structs can be more performant by avoiding defensive copies in readonly contexts (like when passed by `in`, when stored in readonly field, `ref readonly` variables etc):

```csharp
// Struct 'MyStruct' can be made readonly
public struct MyStruct
//            ~~~~~~~~
{
    private readonly long _x;
    private readonly long _y;
    public void FooBar() { }
}
```

All the analyzers below will trigger a diagnostics only for "large" structs, i.e. with the structs larger than 16 bytes. This is done to avoid too many warnings when there is potential performance issues because the copy of a small struct will be very much negligible.

### Non-readonly struct is passed as in-parameter
It doesn't make sense to pass non-readonly and non-poco structs as `in` parameter, because almost every access to the argument will cause a defensive copy that will eliminate all the benefits of "passing by reference":

```csharp
// Non-readonly struct 'MyStruct' is passed as in-parameter 'ms'
public static void InParameter(in MyStruct ms)
//                             ~~~~~~~~~~~~~~
{
}

// Non-readonly struct 'MyStruct' returned by readonly reference
public static ref readonly MyStruct Return() => throw null;
//            ~~~~~~~~~~~~~~~~~~~~~
```

### Defensive copy analyzers
C# 7.3 introduced features that help passing or returning struct by "readonly" reference. The features are very helpful for readonly structs, but for non-readonly members of non-readonly structs that can decrease performance by causing a lots of redundant copies.

```csharp
public static void HiddenCopy(in MyStruct ms)
{
    // Some analyzers are triggered only for "large" structs to avoid extra noise
    // Hidden copy copy
    
    // Expression 'FooBar' causes a hidden copy of a non-readonly struct 'MyStruct'
    ms.FooBar();
    // ~~~~~~

    ref readonly MyStruct local = ref ms;

    // Hidden copy as well
    local.FooBar();
    //    ~~~~~~

    // Hidden copy as well
    _staticStruct.FooBar();
    //            ~~~~~~
}

private static readonly MyStruct _staticStruct;
```

# Contributions
Are highly appreciated. You may send a pull request with various fixes or you can suggest some interesting rule that can prevent from some nasty bugs in your app!

# HowTos
Q: How to generate stable nuget packages that can be added to a local nuget feed?
A: msbuild /p:PublicRelease=true

Q: How to add a newly generate nuget package into a local nuget feed?
A: nuget add $package -source c:\localPackages

# Roadmap

TBD
