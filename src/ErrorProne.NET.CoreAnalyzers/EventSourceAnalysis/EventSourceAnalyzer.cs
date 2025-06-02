using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection.Metadata;
using ErrorProne.NET.Core;
using ErrorProne.NET.CoreAnalyzers;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using OperationExtensions = ErrorProne.NET.Core.OperationExtensions;

namespace ErrorProne.NET.EventSourceAnalysis
{
    /// <summary>
    /// An analyzer that warns on incorrect event sources.
    /// </summary>
    /// <remarks>
    /// The rules EventSource classes should follow:
    /// (see https://github.com/microsoft/dotnet-samples/blob/master/Microsoft.Diagnostics.Tracing/EventSource/docs/EventSource.md)
    /// 1. Classes should be sealed
    ///    Note, a custom event source might not have 'EventSource' attribute. This is not required.
    /// 2. All instance methods assumed to log data unless they are marked with the [NonEvent] attribute.
    /// 3. The same event id is passed in more than one method
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EventSourceAnalyzer : DiagnosticAnalyzerBase
    {
        private record EventMethodInfo(
            object EventId,
            IMethodSymbol MethodSymbol)
        {
            public ImmutableArray<IParameterSymbol> Parameters => MethodSymbol.Parameters;
        };

        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.ERP042;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public EventSourceAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
            context.RegisterOperationAction(AnalyzeMethodBody, OperationKind.MethodBody);
        }

        private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (context.ContainingSymbol is INamedTypeSymbol classType && classType.IsEventSourceClass(context.Compilation))
            {
                // Checking for duplicate ids.
                var methods = classType.GetMembers().OfType<IMethodSymbol>().Select(m => TryGetEventMethod(m, context.Compilation)).Where(m => m != null).ToList();
                var duplicateIds = methods.GroupBy(m => m!.EventId).Where(g => g.Count() > 1).ToList();
                foreach (var duplicate in duplicateIds)
                {
                    // We should warn on all the methods but the first one.
                    // This is might not be deterministic, but I don't think it matters that much.
                    var first = duplicate.First()!;
                    foreach (var m in duplicate.Skip(1))
                    {
                        // It is possible that the class is partial and in this case we only want to report the diagnostics once.
                        // To do that, we're going to check if the method is declared in the class we're analyzing.

                        var methodSyntax = m!.MethodSymbol.TryGetDeclarationSyntax();
                        if (methodSyntax is null)
                        {
                            continue;
                        }

                        if (!classType.IsPartialDefinition() || methodSyntax.EnumerateParents().Any(p => p == context.Node))
                        {
                            var location = methodSyntax.Identifier.GetLocation();

                            var className = classType.Name;
                            var message = $"Event {m.MethodSymbol.Name} has ID {m.EventId} is already in use by event {first.MethodSymbol.Name}.";
                            context.ReportDiagnostic(Diagnostic.Create(Rule, location, className, message));
                        }
                    }
                }
            }
        }

        private static object? GetConstantFrom(IOperation? operation)
        {
            if (operation is ILiteralOperation literal)
            {
                return literal.ConstantValue.Value;
            }

            if (operation is IFieldReferenceOperation fieldReference)
            {
                return fieldReference.ConstantValue.Value;
            }

            if (operation is IConversionOperation conversion)
            {
                return conversion.ConstantValue.Value;
            }

            return null;
        }

        private static Diagnostic CreateDiagnostic(Location location, string className, string message)
        {
            return Diagnostic.Create(Rule, location, className, message);
        }

        private void AnalyzeMethodBody(OperationAnalysisContext context)
        {
            var methodSymbol = (IMethodSymbol)context.ContainingSymbol;
            if (!methodSymbol.ContainingType.IsEventSourceClass(context.Compilation))
            {
                return;
            }

            var methodBody = (IMethodBodyOperation)context.Operation;
            var eventSourceType = methodSymbol.ContainingType.Name;

            if (context.ContainingSymbol is IMethodSymbol method)
            {
                var eventMethodInfo = TryGetEventMethod(method, context.Compilation);
                if (eventMethodInfo == null)
                {
                    // This is not an event method. Ignore!
                    return;
                }

                var hasRelatedActivityId = eventMethodInfo.Parameters.Any(p => p.Type.IsClrType(context.Compilation, typeof(Guid)));

                // Checking if the parameter types are compatible with ETW
                foreach (var p in eventMethodInfo.Parameters)
                {
                    if (!p.Type.IsSupportedParameterType(context.Compilation))
                    {
                        var error =
                            $"Event {method.Name} has an unsupported type '{p.Type.Name}' for parameter '{p.Name}'.";
                        var paramSyntax = p.DeclaringSyntaxReferences.FirstOrDefault();
                        var location = paramSyntax?.GetSyntax().GetLocation();
                        context.ReportDiagnostic(
                            CreateDiagnostic(location ?? methodBody.Syntax.GetLocation(), eventSourceType, error));
                    }
                }

                var foundWriteEvent = false;

                var invocations = context.Operation.Descendants().Where(c => c is IInvocationOperation).ToList();
                foreach (var i in invocations)
                {
                    var invocation = (IInvocationOperation)i;
                    var targetMethodName = invocation.TargetMethod.Name;
                    var argumentList = invocation.FlattenArguments();
                    if (targetMethodName is "WriteEvent" or "WriteEventCore" or "WriteEventWithRelatedActivityId" or "WriteEventWithRelatedActivityIdCore")
                    {
                        foundWriteEvent = true;
                        var eventId = argumentList.FirstOrDefault(a => a.ParameterName == "eventId");
                        if (eventId.ParameterName is null)
                        {
                            // We can't determine the event id, so we can't check it.
                            break;
                        }

                        // Checking the Id mismatch.
                        var eventIdValue = GetConstantFrom(eventId.Operation);
                        if (!eventMethodInfo.EventId.Equals(eventIdValue))
                        {
                            var error =
                                $"Event {method.Name} is given event ID {eventMethodInfo.EventId} but {eventIdValue ?? eventId.Operation.Syntax.ToString()} was passed to {targetMethodName}";
                            context.ReportDiagnostic(CreateDiagnostic(invocation.Syntax.GetLocation(), eventSourceType, error));
                            break;
                        }

                        // The WriteEventCore method is a bit more complicated
                        if (targetMethodName.EndsWith("Core"))
                        {
                            // Checking the argument count only
                            var eventDataCountOperation = argumentList.FirstOrDefault(a => a.ParameterName == "eventDataCount").Operation;
                            var argCount = GetConstantFrom(eventDataCountOperation);

                            if (argCount is int count && count != eventMethodInfo.Parameters.Length)
                            {
                                var error =
                                    $"Event {method.Name} written with payload Event {eventIdValue} was called with {invocation.Arguments.Length - 1} argument(s), but it is defined with {eventMethodInfo.Parameters.Length} parameter(s)";
                                context.ReportDiagnostic(CreateDiagnostic(invocation.Syntax.GetLocation(), eventSourceType, error));
                            }

                            break;
                        }

                        // Checking that the Guid parameter is named correctly
                        if (targetMethodName.Contains("WithRelatedActivityId"))
                        {
                            var guids = eventMethodInfo.Parameters
                                .Where(p => p.IsClrType(context.Compilation, typeof(Guid))).ToList();

                            var firstParam = eventMethodInfo.Parameters.FirstOrDefault();
                            if (firstParam is null ||
                                !firstParam.Type.IsClrType(context.Compilation, typeof(Guid)) ||
                                firstParam.Name != "relatedActivityId")
                            {
                                var methodLocation = eventMethodInfo.MethodSymbol.TryGetDeclarationSyntax()?.Identifier.GetLocation();
                                var error =
                                    $"The first parameter of event method {method.Name} to be of type Guid and to be named \"relatedActivityId\" when calling {targetMethodName}.";
                                context.ReportDiagnostic(
                                    CreateDiagnostic(methodLocation ?? invocation.Syntax.GetLocation(), eventSourceType, error));
                                break;
                            }
                        }

                        // Checking for params mismatch.
                        if (eventMethodInfo.Parameters.Length != argumentList.Count - 1)
                        {
                            // The runtime error:
                            // Event EventSourceMessage written with payload Event 1 was called with 3 argument(s) , but it is defined with 2 paramenter(s)
                            // Using Args - 1, since the first argument is an event id.
                            var error =
                                $"Event {method.Name} written with payload Event {eventIdValue} was called with {argumentList.Count - 1} argument(s), but it is defined with {eventMethodInfo.Parameters.Length} parameter(s)";
                            context.ReportDiagnostic(CreateDiagnostic(invocation.Syntax.GetLocation(), eventSourceType, error));
                            break;
                        }

                        // Technically, there is an interesting issue:
                        // the following code "works":
                        // [Event(1)]
                        // public void AppStarted2(short n, int k) => WriteEvent(1, "1142", n);
                        // The result is strange, but nothing crashes.
                        // For now, checking params count only.
                        // using simple check by name.
                        var arguments = argumentList.Select(a => ReferencedParameter(a) ?? "").ToImmutableHashSet();
                        foreach (var parameter in eventMethodInfo.Parameters)
                        {
                            if (!arguments.Contains(parameter.Name))
                            {
                                var error =
                                    $"Event {method.Name} does not pass parameter '{parameter.Name}' to {targetMethodName}.";
                                context.ReportDiagnostic(
                                    CreateDiagnostic(invocation.Syntax.GetLocation(), eventSourceType, error));
                            }
                        }


                    }

                    if (foundWriteEvent)
                    {
                        // We potentially might have more than one call to WriteEvent method.
                        // This is super strange, but we analyze the method call only once.
                        break;
                    }
                }

                if (!foundWriteEvent)
                {
                    var methodNames = hasRelatedActivityId ? "WriteEventWithRelatedActivityId or WriteEventWithRelatedActivityIdCore" : "WriteEvent or WriteEventCore";
                    var error = $"Event {method.Name} does not call {methodNames}.";
                    context.ReportDiagnostic(Diagnostic.Create(Rule, methodSymbol.TryGetDeclarationSyntax()?.Identifier.GetLocation() ?? methodBody.Syntax.GetLocation(), methodBody.Type?.Name, error));
                }
            }
        }

        private static string? ReferencedParameter(OperationExtensions.ArgumentInfo argument)
        {
            return argument.Operation.EnumerateChildOperations().OfType<IParameterReferenceOperation>()
                .Select(p => p.Parameter.Name).FirstOrDefault();
        }

        private static EventMethodInfo? TryGetEventMethod(IMethodSymbol method, Compilation compilation)
        {
            var attributes = method.GetAttributes();
            if (attributes.Any(a => a.AttributeClass?.IsClrType(compilation, typeof(NonEventAttribute)) == true))
            {
                return null;
            }

            // EventId is either provided in 'EventAttribute' or computed based on the method order.
            var eventAttribute =
                attributes.FirstOrDefault(a => a.AttributeClass?.IsClrType(compilation, typeof(EventAttribute)) == true);
            if (eventAttribute == null)
            {
                // From the docs: "Any instance, non-virtual, void returning method defined in an event source class is by default an ETW event method."
                if (!method.IsStatic && !method.IsVirtual && method.ReturnsVoid && !method.IsConstructor() && method.MethodKind != MethodKind.Destructor)
                {
                    // In this case the Id is inferred.
                    // "Implicitly: by the ordinal number of the method in the class (thus the first method in the class is 1, second 2 …)"
                    var methods = method.ContainingType.GetMembers().OfType<IMethodSymbol>().Where(m => !m.IsStatic && !m.IsVirtual && m.ReturnsVoid).ToList();
                    
                    var index = methods.IndexOf(method) + 1;
                    return new EventMethodInfo(EventId: index, MethodSymbol: method);
                }

                return null;
            }

            if (eventAttribute.ConstructorArguments.Length == 0)
            {
                // Strange, we should have at least one argument.
                return null;
            }

            var eventId = eventAttribute.ConstructorArguments[0].Value;
            if (eventId is null)
            {
                return null;
            }

            return new EventMethodInfo(eventId, method);
        }

        private static IOperation GetExpectedEventId(IInvocationOperation invocation)
        {
            return null!;
        }


        /// <summary>
        /// Returns true if 
        /// </summary>
        /// <param name="invocation"></param>
        /// <returns></returns>
        private static bool IsEventMethod(IInvocationOperation invocation)
        {
            //IMethodBodyOperation
            return true;
        }



        private void AnalyzeInvocationOperation(OperationAnalysisContext context)
        {
            // Looking for 'Enumerable.Contains' call that passes an extra argument of type 'StringComparer'.
            var invocationOperation = (IInvocationOperation)context.Operation;

            var receiverType = invocationOperation.GetReceiverType(includeLocal: true);

            if (
                IsEnumerableContains(invocationOperation.TargetMethod, context.Compilation) &&
                IsSet(receiverType, context.Compilation))
            {
                var diagnostic = Diagnostic.Create(Rule, invocationOperation.Syntax.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }

            static bool IsEnumerableContains(IMethodSymbol methodSymbol, Compilation compilation)
            {
                return methodSymbol.Name == nameof(Enumerable.Contains) &&
                       // There are two overloads, one that just takes the value
                       // and the second one that takes StringComparer,
                       // and only the second one is linear.
                       methodSymbol.Parameters.Length == 3 &&
                       methodSymbol.ContainingType.IsClrType(compilation, typeof(Enumerable));
            }

            static bool IsSet(ITypeSymbol? typeSymbol, Compilation compilation)
            {
                if (typeSymbol is not INamedTypeSymbol { IsGenericType: true } nt)
                {
                    return false;
                }

                return typeSymbol.IsGenericType(compilation, typeof(HashSet<>)) ||
                       typeSymbol.IsGenericType(compilation, typeof(ISet<>)) ||
                       nt.ConstructedFrom
                           .AllInterfaces
                           .Any(i => i.IsGenericType(compilation, typeof(ISet<>)));
            }
        }
    }
}