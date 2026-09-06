using System;

namespace ErrorProne.Samples.CoreAnalyzers;

public class RecursiveCallSample
{
    // EPC30: clearly unconditional self-recursion -> warns.
    public void AlwaysRecurses()
    {
        AlwaysRecurses(); // ❌ EPC30
    }

    // EPC30: the guard 'b' is passed unchanged, so once taken it recurses forever.
    public void InvariantGuard(bool b)
    {
        if (b) InvariantGuard(b); // ❌ EPC30
    }

    // OK: a conditional can terminate the recursion before the call.
    public void ConditionalEarlyReturn(int n)
    {
        n++;
        if (n > 10) return;
        ConditionalEarlyReturn(n); // ✅ no warning
    }

    // OK: the argument changes, so the guard is not invariant.
    public void DecreasingArgument(int n)
    {
        if (n > 0)
        {
            n--;
            DecreasingArgument(n); // ✅ no warning
        }
    }

    private bool _done;

    // OK: termination depends on instance state.
    public void DependsOnInstanceState()
    {
        if (_done) return;
        DependsOnInstanceState(); // ✅ no warning
    }

    // OK: a proper base case with a changing argument.
    public int Factorial(int n)
    {
        if (n <= 1) return 1;
        return n * Factorial(n - 1); // ✅ no warning
    }
}
