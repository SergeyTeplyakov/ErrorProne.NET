using System;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Core;

[Flags]
public enum TaskLikeTypes
{
    Task = 1 << 0,
    TaskOfT = 1 << 1,
    ValueTask = 1 << 2,
    ValueTaskOfT = 1 << 3,
    TasksOnly = Task | TaskOfT,
    All = Task | TaskOfT | ValueTask
}

public record TaskLikeTypesHolder(
        INamedTypeSymbol? TaskType,
        INamedTypeSymbol? TaskOfTType,
        INamedTypeSymbol? ValueTaskType,
        INamedTypeSymbol? ValueTaskOfTType);

public static class TaskTypeExtensions
{
    public static TaskLikeTypesHolder GetTaskTypes(Compilation compilation)
    {
        var taskType = compilation.TaskType();
        var taskOfTType = compilation.TaskOfTType();
        var valueTaskType = compilation.ValueTaskType();
        var valueTaskOfTType = compilation.ValueTaskOfTType();

        return new(taskType, taskOfTType, valueTaskType, valueTaskOfTType);
    }

    public static bool IsTaskLike(this ITypeSymbol? returnType, Compilation compilation, TaskLikeTypes typesToCheck)
    {
        if (returnType == null)
        {
            return false;
        }

        var (taskType, taskOfTType, valueTaskType, valueTaskOfTType) = GetTaskTypes(compilation);
        if (taskType == null || taskOfTType == null)
        {
            return false; // ?
        }

        if ((typesToCheck & TaskLikeTypes.Task) != 0 && returnType.Equals(taskType, SymbolEqualityComparer.Default))
        {
            return true;
        }

        if ((typesToCheck & TaskLikeTypes.TaskOfT) != 0 && returnType.OriginalDefinition.Equals(taskOfTType, SymbolEqualityComparer.Default))
        {
            return true;
        }

        if ((typesToCheck & TaskLikeTypes.ValueTask) != 0 && returnType.Equals(valueTaskType, SymbolEqualityComparer.Default))
        {
            return true;
        }
        
        if ((typesToCheck & TaskLikeTypes.ValueTaskOfT) != 0 && returnType.OriginalDefinition.Equals(valueTaskOfTType, SymbolEqualityComparer.Default))
        {
            return true;
        }

        if (returnType.IsErrorType())
        {
            return returnType.Name.Equals("Task") ||
                   returnType.Name.Equals("ValueTask");
        }

        return false;
    }

    public static bool IsTaskLike(this ITypeSymbol? returnType, Compilation compilation) =>
        IsTaskLike(returnType, compilation, TaskLikeTypes.All);
}