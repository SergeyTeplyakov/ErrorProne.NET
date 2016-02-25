# Error Prone .NET

ErrorProne.NET is a set of Roslyn-based analyzers that will help you to write correct code. The idea is similar to Google's [error-prone](https://github.com/google/error-prone) but focusing on correctness (and, maybe, performance) of C# programs.

Current implementation supports various rules that helps to prevent common coding errors.

## Non-observed return value for pure methods

Result for every call to side-effect free method should be observed in one or another way. If such a method is invoked without observing the result, then it could lead to undesired behavior of the program.
ErrorProne.NET has some special rules and heuristics to detect such invocations. For instance, calls to method marked with `PureAttribute` should be observed. The same is true for well-known immutable types like `object`, `IEnumerable` etc. All method invocations for extensions method of such immutable types considered pure as well.

Here is a short list that shows this rule on practice. Every line ended with `// Non-Observed return value` produces the warning: 

```csharp
// Linq methods
Enumerable.Range(1, 10);
Enumerable.Range(1, 5).Select(x => x.ToString()).FirstOrDefault();

// Third-party extensions methods for IEnumerable<T>
new int[] { 1, 2 }.ToImmutableList(); // Non-Observed return value

// Methods on string
"x".Substring(1); // Non-Observed return value

// On pure methods
PureMethod(); // Non-Observed return value

// member of all immutable collections
var list = Enumerable.Range(1, 10).ToImmutableList();
list.Add(42); // Non-Observed return value

// On With pattern
var tree = CSharpSyntaxTree.ParseText("class Foo {}");
tree.WithFilePath("path"); // Non-Observed return value

// Calls to well-known system types
Convert.ToByte(42); // Non-Observed return value
ToString(); // Non-Observed return value
object.ReferenceEquals(null, 42); // Non-Observed return value
IComparable<int> n = 42; 
n.CompareTo(3); // Non-Observed return value

// Static methods on well-known structs
char.IsDigit('c');
int.Parse("foo");
```

## Assignment-free object construction
In many cases object creation via `new` expression also considered pure and such invocation should be observed in some way. Unfortunately, there is no simple way to analyze whether constructor has additional side effects or not, but in some cases it is known for sure that side effects are absent. For instance, "default constructors" on structs could not have any side effects, and the same is true for all immutable types and for some mutable types like collections.

Following code snippet shows this rule in action:

```csharp
// Assignment free construction of well-known primitive types
new object(); // Assignment-free object construction
new string('c', 42); // Assignment-free object construction

// default constructor for structs
new int(); // Assignment-free object construction
// Including custom structs
new CustomStruct();

// Default constructor for enums
new CustomEnum();

// Default constructor for all collections
new List<int>();
```

## Assignment-free exception creation
Exception creation could be considered side-effect free as well, but because of its special nature it deserves it's own rule. Assignment-free, throw-free exception creation will lead to an error by this tool:

```csharp
// Standalone exception creation is an error!
new Exception();
``` 

## Rules for validating formatting string
Another common source of errors - invalid format argument for such methods like `string.Format`, `Console.WriteLine` etc. There is 3 types of errors: 1) expected argument was not provided 2) argument was provided but was not used in the format string and 3) format string is invalid. ErrorProne.NET checks all of them (please keep in mind, that excessive arguments will not lead to runtime failure but they're considered very suspicious as well).

```csharp
// Format argument was not provided

// Argument 3 was not provided
Console.WriteLine("{0}, {3}", 1);
// Argument 2 was not provided
var s = string.Format(format: "{2}", arg0: 1);
// Argument 1 was not provided
WriteLog("{1}", 1);

// Excessive arguments

// Argument 3 was not used in the format string
// Rule is working for const fields variables and
// with static readonly fields/properties
const string format = "{0}, {1}";
Console.WriteLine(format, 1, 2, 3);

// Format argument is a valid format string
s = string.Format("{1\d(");
```

ErrorProne.NET recognizes custom format methods that marked with `JetBrains.Annotations.StringFormatMethodAttribute`:

```csharp
namespace JetBrains.Annotations
{
    public class StringFormatMethodAttribute : Attribute
    {
        public StringFormatMethodAttribute(string name) { }
    }
}

public class StringFormatAnalysis
{
	[JetBrains.Annotations.StringFormatMethod("message")]
	public static void WriteLog(string message, params object[] args) { }

	public void Sample()
	{
		// Argument 1 was not provided
		WriteLog("{1}", 1);
	}
}
```

ErrorProne.NET also has special rule that checks regex validity. So following code will trigger an error:

```csharp
// Regex pattern is invalid: parsing "\d(" - Not enough )'s.
var regex = new Regex("\\d(");
```

## Switch completeness analysis
ErrorProne.NET has special rule that enforces completeness of the switch statement over variable of enum type. Consider following example:

```csharp
enum ShapeType
{
    Circle,
    Rectangle,
    Square
}
abstract class Shape
{
    public static Shape CreateShape(ShapeType shapeType)
    {
        // Warning: Possible missed enum case(s) 'Square' int the switch statement
        switch(shapeType)
        {
            case ShapeType.Circle: return new Circle();
            case ShapeType.Rectangle:return new Rectangle();
            default: throw new InvalidOperationException($"Unknown shape type '{shapeType}'");
        }
    }
}
class Circle : Shape { }
class Rectangle : Shape { }
``` 

ErrorProne.NET recognizes that default section of the switch statement throws `InvalidOperationException` that could be a marker that all the cases are checked by the switch. If this is not the case warning would be emitted that some cases were potentially missed.

## Exception handling rules
ErrorProne.NET has few rules that enforces exception handling best practices.

### Incorrect exception propagation
Incorrect exception propagation with `throw ex` is probably most well-known issue that any developer can face in any project. `throw ex` will change the original exception stack tract that can complicates further diagnostic a lot.

```csharp
// Incorrect exception propagation
try { throw new Exception(); }
catch (Exception e)
// Incorrect exception propagation. Use throw; instead
{ throw e; }
```

### Suspicious exception observation
Another common mistake that can significantly complicate production postmortem is related to incorrect observation of the exception. Catching `System.Exception` considered harmful because it can leave system in unpredictable state, but even when base exception is caught it is very important that full exception object (including stack trace and inner exception) is observed.

Consider following case:

```csharp
class WillThrow
{
    public WillThrow()
    {
        throw new Exception("Oops!");
    }
}

public static T Create<T>() where T : new()
{
    return new T();
}

public void Sample()
{
    try { Create<WillThrow>(); }
    // Warn for ex.Message: Only ex.Message was observed in the exception block!
    catch(Exception ex) { Console.WriteLine(ex.Message); }
}
```

Due to implementation details of the C# programming language, every exception thrown during object construction in would be wrapped into `TargetInvocationException`. This aspect will lead to very obscure error message: `Exception has been thrown by the target of an invocation` without any hints of the root cause of the problem. To avoid such kind of problems ErrorProne.NET will warn if only `ex.Message` was observed in exception handler that takes `System.Exception`.

### Swallowing base exceptions considered harmful

Another common bad practice in exception handling is swallowing all exception by using `catch(System.Exception)` or `catch {}`. To avoid swallowing exceptions, ErrorProne.NET has a rule that will check exit points of the catch block and warn if exception was swallowed without observing it's state:

```csharp
try { throw new Exception(); }
catch (Exception e)
{
    if (e is AggregateException) return;
} // Exit point '}' swallows unobserved exception
```

Please note, that this rule will warn only when exception object was not observed (like printed to console, log etc).

# Contributions
Are highly appreciated. You may send a pull request with various fixes or you can suggest some interesting rule that can prevent from some nasty bugs in your app!

# Roadmap

* Add `Immutable` attribute that will enforce type immutability
* Add well known attribute like `NoExceptionSwallowing` that will enforce some rules related to exception handling
* Add `NoAdditionalHeapAllocations` attribute that will warn on uncesseary boxing operations on different levels (method, class, assembly)
* Warn on using `ToString` on collections
* Warn on using `Equals`, `GetHashCode` on collections
* Add a `Record` attribute that will enforce that all class/struct fields are used in `ToString`/`GetHashCode` and `ToString` methods
* Make pure-method rule extensibile (external annotations? use attributes from Code Contracts repo?) 
