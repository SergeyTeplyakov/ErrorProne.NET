# EPC41 - Format string does not match arguments (user-annotated formatting methods)

Reports calls to user-annotated formatting methods where the format string references argument
indices that are not actually supplied. Catches the same class of bug as
[CA2241](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2241)
(which would throw `FormatException` at runtime) but for **third-party** methods you do not own and
cannot mark with attributes.

## Description

`String.Format`-style methods take a format string with placeholders like `{0}`, `{1,10:N2}`. When
a placeholder index is greater than or equal to the number of supplied arguments, the runtime throws
`FormatException`. CA2241 catches this for a fixed list of BCL methods. EPC41 lets you extend the
analysis to your own / third-party libraries by listing them in `.editorconfig`.

EPC41 ships **no** built-in method list — it does nothing until you annotate at least one method.
Use CA2241 for BCL methods; use EPC41 for everything else.

## Configuration

Add the methods to analyze under `dotnet_diagnostic.EPC41.format_methods` in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.EPC41.format_methods =
    MyCorp.Logging.Logger.LogInfo:0;
    MyCorp.Logging.Logger.LogError:1;
    MyCorp.Logging.Logger.Log*:0;
    MyCorp.Logging.Logger.Trace:format;
    SomeLib.Tracer.Trace:0
```

Each entry has the form `FullyQualifiedTypeName.MethodName[*]:(index|paramName)`:

- `FullyQualifiedTypeName` — namespace-qualified containing-type name (no `global::` prefix). For
  generic types, use the unbound name (e.g. `MyCorp.Cache`, not `MyCorp.Cache<T>`).
- `MethodName` — the method name. Matches **all overloads** of that name on that type. A trailing
  `*` makes it a prefix match (e.g. `Log*` matches `Log`, `LogInfo`, `LogError`, ...). Exact
  matches take precedence over wildcards.
- `index` — 0-based index of the parameter holding the format string, **or**
- `paramName` — the name of the parameter holding the format string (resolved per overload, so
  this works even when the format parameter has different ordinals across overloads).

Entries can be separated by `;`, `,`, or newlines. Whitespace is ignored.

### Example signatures and corresponding spec

```csharp
public static void Log(string format, params object[] args);                    // → :0 or :format
public static void Log(IFormatProvider p, string format, params object[] args); // → :1 or :format
public static void Trace(int level, string template, object a, object b);       // → :1 or :template
```

## What is reported

For each matching call where the **format string is a compile-time constant**:

1. **Placeholder index out of range** — e.g. format `"{0} {1}"` with only one trailing argument.
2. **Malformed format string** — unbalanced `{` or `}`, non-numeric placeholder body, missing `}`.

## What is NOT reported

- Calls where the format string is not a compile-time constant (e.g. `Log(fmt, x)` where `fmt` is a
  parameter or field).
- Calls where the `params` argument is an opaque `object[]` variable (we cannot count its elements
  statically). Explicit `new object[] { ... }` literals **are** counted.
- "Too many arguments" — unused trailing arguments are not flagged (intentional: matches CA2241).
- Methods not listed in `format_methods`.
- Structured-logging templates (`{UserName}`-style named placeholders, à la Serilog or
  `Microsoft.Extensions.Logging`). EPC41 only understands numeric `{N}` placeholders today.

## Examples

Given this configuration:

```ini
dotnet_diagnostic.EPC41.format_methods = MyCorp.Logging.Logger.Log:0
```

### ❌ Reported

```csharp
Logger.Log("{0} {1}", 1);                  // {1} has no argument
Logger.Log("value = {0}");                 // no args at all
Logger.Log("unterminated {0", 1);          // malformed
Logger.Log("stray } here", 1);             // malformed
Logger.Log("{0} {1} {2}", new object[] { 1, 2 }); // counted: 2 < 3
```

### ✅ Not reported

```csharp
Logger.Log("{0} and {1}", 1, 2);           // matches
Logger.Log("no placeholders here");        // no placeholders
Logger.Log("{{not a placeholder}} {0}", 1); // escaped braces
Logger.Log(GetFormat(), 1, 2);             // non-constant format
Logger.Log("{0} {1} {2}", existingArray);  // params array is opaque
```

## How to fix

- Add the missing argument(s), or remove the offending placeholder.
- For "off-by-one" mistakes, double-check the indices: placeholders are 0-based but it's easy to
  read `"{1}"` and reach for the first argument.
- If the format string is dynamically constructed and the analyzer is wrong about a corner case,
  suppress at the call site:

  ```csharp
  #pragma warning disable EPC41 // dynamically resolved via X
  ```

## See also

- [CA2241 - Provide correct arguments to formatting methods](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2241)
  — the BCL-targeted equivalent. EPC41 deliberately omits the BCL list so there is no double-warning.
