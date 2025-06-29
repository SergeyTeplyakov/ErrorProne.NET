using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET
{
    internal static class DiagnosticDescriptors
    {
        private const string CodeSmellCategory = "CodeSmell";
        private const string PerformanceCategory = "Performance";
        private const string ConcurrencyCategory = "Concurrency";
        private static readonly string[] UnnecessaryTag = new [] { WellKnownDiagnosticTags.Unnecessary };

        /// <nodoc />
        internal static readonly DiagnosticDescriptor EPC11 = new DiagnosticDescriptor(
            nameof(EPC11), 
            title: "Suspicious equality implementation", 
            messageFormat: "Suspicious equality implementation: {0}", 
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "Equals method that does not use any instance members or other instance is suspicious.");

        /// <nodoc /> 
        public static readonly DiagnosticDescriptor EPC12 = new DiagnosticDescriptor(
            nameof(EPC12), title: "Suspicious exception handling: only the 'Message' property is observed in the catch block",
            messageFormat: "Suspicious exception handling: only '{0}.Message' is observed in the catch block",
            category: CodeSmellCategory, defaultSeverity: DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "In many cases the 'Message' property contains irrelevant information and actual data is kept in inner exceptions.",
            // The diagnostics fades parts of the code.
            customTags: UnnecessaryTag);

        /// <nodoc />
        internal static readonly DiagnosticDescriptor EPC13 = new DiagnosticDescriptor(
            nameof(EPC13), 
            title: "Suspiciously unobserved result", 
            messageFormat: "The result of type '{0}' should be observed", 
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "Return values of some methods should always be observed.");

        /// <nodoc />
        internal static readonly DiagnosticDescriptor EPC14 = new DiagnosticDescriptor(
            nameof(EPC14), 
            title: "ConfigureAwait(false) call is redundant", 
            messageFormat: "ConfigureAwait(false) call is redundant", 
            CodeSmellCategory, 
            // Using Info to fade away the call, not warn on it.
            DiagnosticSeverity.Info, isEnabledByDefault: true, 
            description: "The assembly is configured not to use .ConfigureAwait(false).",
            // The diagnostics fades parts of the code.
            customTags: UnnecessaryTag);

        /// <nodoc />
        internal static readonly DiagnosticDescriptor EPC15 = new DiagnosticDescriptor(
            nameof(EPC15), 
            title: "ConfigureAwait(false) must be used", 
            messageFormat: "ConfigureAwait(false) must be used", 
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "The assembly is configured to use .ConfigureAwait(false).");

        /// <nodoc />
        internal static readonly DiagnosticDescriptor EPC16 = new DiagnosticDescriptor(
            nameof(EPC16), 
            title: "Awaiting a result of a null-conditional expression will cause NullReferenceException", 
            messageFormat: "Awaiting a result of a null-conditional expression will cause NullReferenceException", 
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "Null-conditional operator returns null when 'lhs' is null, causing NRE when a task is awaited.");

        /// <nodoc />
        internal static readonly DiagnosticDescriptor EPC17 = new DiagnosticDescriptor(
            nameof(EPC17), 
            title: "Avoid async-void delegates", 
            messageFormat: "Avoid async-void delegates", 
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "Async-void delegates can cause application to crash.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC18 = new DiagnosticDescriptor(
            nameof(EPC18), 
            title: "A task instance is implicitly converted to a string", 
            messageFormat: "A task instance is implicitly converted to a string", 
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "An implicit conversion of a task instance to a string potentially indicates an lack of 'await'.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC19 = new DiagnosticDescriptor(
            nameof(EPC19), 
            title: "Observe and Dispose a 'CancellationTokenRegistration' to avoid memory leaks", 
            messageFormat: "Observe and Dispose a 'CancellationTokenRegistration' to avoid memory leaks", 
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "Failure to dispose 'CancellationTokenRegistration' may cause a memory leak if obtained from a non-local token.");
        
        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC20 = new DiagnosticDescriptor(
            nameof(EPC20), 
            title: "Avoid using default ToString implementation", 
            messageFormat: "A default ToString implementation is used for type {0}", 
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "A default ToString implementation is rarely gives the result you need.");

        /// <nodoc />
        internal static readonly DiagnosticDescriptor ERP021 = new DiagnosticDescriptor(
            nameof(ERP021), 
            title: "Incorrect exception propagation", 
            messageFormat: "Incorrect exception propagation. Use 'throw;' instead.", 
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "Incorrect exception propagation alters the stack trace of a thrown exception.");

        /// <nodoc />
        internal static readonly DiagnosticDescriptor ERP022 = new DiagnosticDescriptor(
            nameof(ERP022), 
            title: "Unobserved exception in a generic exception handler", 
            messageFormat: "An exit point '{0}' swallows an unobserved exception", 
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "A generic catch block swallows an exception that was not observed.");
        
        /// <nodoc />
        internal static readonly DiagnosticDescriptor EPC23 = new DiagnosticDescriptor(
            nameof(EPC23), 
            title: "Avoid using Enumerable.Contains on HashSet<T>", 
            messageFormat: "Linear search via Enumerable.Contains is used instead of an instance Contains method", 
            PerformanceCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "Enumerable.Contains is less efficient since it scans all the entries in the hashset and allocates an iterator.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC24 = new DiagnosticDescriptor(
            nameof(EPC24),
            "A hash table \"unfriendly\" type is used as the key in a hash table",
            "A struct '{0}' with a default {1} implementation is used as a key in a hash table",
            category: PerformanceCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "The default implementation of 'Equals' and 'GetHashCode' for structs is inefficient and could cause severe performance issues.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC25 =
            new DiagnosticDescriptor(nameof(EPC25),
                title: "Avoid using default Equals or HashCode implementation from structs",
                messageFormat: "The default 'ValueType.{0}' is used in {1}",
                category: PerformanceCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true,
                description: "The default implementation of 'Equals' and 'GetHashCode' for structs is inefficient and could cause severe performance issues.");
        
        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC26 =
            new DiagnosticDescriptor(nameof(EPC26),
                title: "Do not use tasks in using block",
                messageFormat: "A Task was used in using block",
                category: CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true,
                description: "Task implements IDisposable but should not be ever disposed explicitly.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC27 = new DiagnosticDescriptor(
            nameof(EPC27),
            title: "Avoid async void methods",
            messageFormat: "Method '{0}' is 'async void'. Use 'async Task' instead.",
            category: CodeSmellCategory,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Async void methods are dangerous and should be avoided except for event handlers.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC28 = new DiagnosticDescriptor(
            nameof(EPC28),
            title: "Do not use ExcludeFromCodeCoverage on partial classes",
            messageFormat: "'ExcludeFromCodeCoverageAttribute' should not be applied to partial class '{0}' (directly or indirectly)",
            category: CodeSmellCategory,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Applying ExcludeFromCodeCoverageAttribute to a partial class can lead to inconsistent code coverage results.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC29 = new DiagnosticDescriptor(
            nameof(EPC29),
            title: "ExcludeFromCodeCoverageAttribute should provide a message",
            messageFormat: "'ExcludeFromCodeCoverageAttribute' should provide a message explaining the exclusion",
            category: CodeSmellCategory,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Always provide a message when using ExcludeFromCodeCoverageAttribute to document the reason for exclusion.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC30 = new DiagnosticDescriptor(
            nameof(EPC30),
            title: "Method calls itself recursively",
            messageFormat: "Method '{0}' calls itself recursively",
            category: CodeSmellCategory,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Detects when a method calls itself recursively, either conditionally or unconditionally.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC31 = new DiagnosticDescriptor(
            nameof(EPC31),
            title: "Do not return null for Task-like types",
            messageFormat: "Do not return null for Task-like type from method '{0}'",
            category: CodeSmellCategory,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Returning null for a Task-like type may lead to NullReferenceException when the task is awaited. Return Task.CompletedTask instead.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor ERP031 = new DiagnosticDescriptor(
            nameof(ERP031), 
            title: "The API is not thread-safe", 
            messageFormat: "The API is not thread-safe.{0}", 
            ConcurrencyCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "The API is not thread safe and can cause runtime failures.");
        
        /// <nodoc />
        public static readonly DiagnosticDescriptor ERP041 = new DiagnosticDescriptor(
            nameof(ERP041), 
            title: "EventSource class should be sealed", 
            messageFormat: "{0}: {1}",
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "The event source implementation must follow special rules to avoid hitting runtime errors.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor ERP042 = new DiagnosticDescriptor(
            nameof(ERP042),
            title: "EventSource implementation is not correct",
            messageFormat: "{0}: {1}",
            CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "The event source implementation must follow special rules to avoid hitting runtime errors.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC32 = new DiagnosticDescriptor(
            nameof(EPC32),
            title: "TaskCompletionSource should use RunContinuationsAsynchronously",
            messageFormat: "TaskCompletionSource instance should be created with TaskCreationOptions.RunContinuationsAsynchronously",
            category: CodeSmellCategory,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Always use TaskCreationOptions.RunContinuationsAsynchronously when creating TaskCompletionSource to avoid potential deadlocks.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPC33 = new DiagnosticDescriptor(
            nameof(EPC33),
            title: "Do not use Thread.Sleep in async methods",
            messageFormat: "Thread.Sleep should not be used in async methods. Use 'await Task.Delay()' instead.",
            category: CodeSmellCategory,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Thread.Sleep blocks the thread and defeats the purpose of async programming. Use Task.Delay with await instead.");
    }
}