# EPC42 - A member of a data contract is not serializable

Warns when a `[DataMember]` of a `[DataContract]` type has a type that `DataContractSerializer`
cannot serialize.

## Description

`DataContractSerializer` validates the object graph **lazily**: the constructor succeeds and the
failure only happens on the first serialization attempt, with an `InvalidDataContractException`.
This makes the bug very easy to miss — the code compiles, the serializer is created successfully,
and the application blows up in production the first time the payload is actually written.

```csharp
[DataContract]
public class Config
{
    [DataMember]
    public IPAddress Address { get; set; } // EPC42
}

var serializer = new DataContractSerializer(typeof(Config)); // works!
serializer.WriteObject(stream, config);                      // InvalidDataContractException
```

`System.Net.IPAddress` is the canonical example: it is marked with `[Serializable]` on the .NET
Framework, but **not** on .NET Core / .NET 5+. Since it also has no parameterless constructor, it
stopped being data-contract-serializable when the code was ported. `System.Net.IPEndPoint` has the
same problem.

> Note: even a "fixed" `IPAddress` would be problematic — `IPAddress.Loopback`, `IPAddress.Any` and
> `IPAddress.None` return an instance of the private nested type `IPAddress+ReadOnlyIPAddress`,
> which is not serializable either.

## When a type is data-contract-serializable

A type is serializable by `DataContractSerializer` when at least one of the following holds:

- it is marked with `[DataContract]` or `[CollectionDataContract]`;
- it is marked with `[Serializable]`, or implements `ISerializable` / `IXmlSerializable`;
- it is a primitive, a `string`, an enum, a `Guid`, a `DateTime`, a `TimeSpan` etc.;
- it is a **public** type with a **parameterless constructor**.

The last rule is the one that is easy to violate. Note that:

- a **non-public** (`internal`, `private` nested) POCO type is *not* serializable, even though it
  compiles fine and looks perfectly reasonable;
- a **non-public parameterless constructor** *is* good enough — `private Foo() { }` works;
- collection types are not exempt: a custom collection without a default constructor fails with
  `"... is an invalid collection type since it does not have a default constructor"`.

## Records

`record` types follow exactly the same rules, and the positional syntax is a common trap: a
positional record has no parameterless constructor, so it is **not** serializable.

```csharp
public record PositionalRecord(int X);      // no parameterless ctor
public record struct PositionalStruct(int X); // fine: a struct always has one
public record RecordWithBody { public int X { get; set; } }  // fine

[DataContract]
public class Config
{
    [DataMember] public PositionalRecord Bad { get; set; }  // EPC42
    [DataMember] public PositionalStruct Ok { get; set; }
    [DataMember] public RecordWithBody AlsoOk { get; set; }
}
```

Positional members of a `[DataContract] record` are only serialized when they are annotated with
`[property: DataMember]`, and the analyzer follows that rule:

```csharp
[DataContract]
public record Config([property: DataMember] IPAddress Address);  // EPC42

[DataContract]
public record Ignored(IPAddress Address);  // not reported: the member is not a data member
```

## What is reported
For every member of a `[DataContract]` type that is marked with `[DataMember]`, the analyzer checks
the member's type, looking through arrays, `Nullable<T>` and generic type arguments. So all of these
are reported:

```csharp
[DataContract]
public class Config
{
    [DataMember] public IPAddress Address { get; set; }                  // EPC42
    [DataMember] public IPAddress[] Addresses { get; set; }              // EPC42
    [DataMember] public List<IPAddress> More { get; set; }               // EPC42
    [DataMember] public Dictionary<string, IPAddress> Map { get; set; }  // EPC42
    [DataMember] public InternalPoco Poco { get; set; }                  // EPC42: the type is not public
    [DataMember] public NoParameterlessCtor Value { get; set; }          // EPC42: no parameterless ctor
}
```

## What is NOT reported

- Members that are not marked with `[DataMember]` (including the ones marked with
  `[IgnoreDataMember]`) — `DataContractSerializer` ignores them.
- Types that are not marked with `[DataContract]`.
- Members typed as an interface, an abstract class or `object`. The runtime type is unknown at
  compile time, and such cases fail with a different exception (`SerializationException`) that is
  solved by adding `[KnownType]`.
- Members typed as a generic type parameter.
- Delegate-typed members.
- Union-typed members. A `union` compiles to a struct whose state is invisible to
  `DataContractSerializer`, so such a member is silently serialized as empty and the payload is
  lost. This is a known gap: it is tracked separately and requires a Roslyn update first.
- Structs, `record struct`s and records with a parameterless constructor.
- Nested problems: if a `[DataMember]` has a POCO type and *that* type has a bad member, the
  diagnostic is reported on the nested type's own declaration when it is a `[DataContract]`, but the
  analyzer does not walk arbitrary object graphs.

## How to fix

There is no single mechanical fix, pick the one that fits:

1. **Use a serializable surrogate.** Most common for BCL types:

   ```csharp
   [DataContract]
   public class Config
   {
       [DataMember(Name = "Address")]
       private string AddressString { get; set; }

       [IgnoreDataMember]
       public IPAddress Address
       {
           get => IPAddress.Parse(AddressString);
           set => AddressString = value.ToString();
       }
   }
   ```

2. **Exclude the member** — remove `[DataMember]` (or replace it with `[IgnoreDataMember]`) if the
   data does not need to travel over the wire.

3. **Make your own type serializable** — add a (possibly private) parameterless constructor, make
   the type public, or annotate it with `[DataContract]` / `[Serializable]`.

4. **Register a surrogate** via `DataContractSerializer`'s `IDataContractSurrogate` /
   `ISerializationSurrogateProvider` if you cannot change either side. In that case suppress the
   diagnostic:

   ```csharp
   #pragma warning disable EPC42 // A surrogate is registered for IPAddress
   ```
