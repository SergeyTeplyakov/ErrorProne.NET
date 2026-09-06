# EPC30 - Method calls itself recursively

This analyzer detects when a method calls itself recursively, which can lead to stack overflow exceptions if not properly controlled.

## Description

The analyzer warns when a method calls itself recursively in a way that is provably **non-terminating**. It reports two situations:

- The recursive call is reached unconditionally (nothing can terminate the method first).
- The recursive call is guarded only by an *invariant* condition — one composed purely of unchanged value parameters and constants (e.g. `if (b) Foo(b);` or `if (n > 0) Foo(n);`). Once such a branch is taken it is taken forever, so the recursion never ends.

To avoid false positives, the analyzer does **not** warn when:

- A conditional that can terminate the method (e.g. `if (something) return;` or a conditional `throw`) appears before the call.
- Termination depends on instance state, a property, or a method call (e.g. `if (_done) return;`, `if (Flag) Foo();`).
- An argument is changed before the call (e.g. `Foo(n - 1)`, `n--; Foo(n);`), or a `ref` parameter is modified before the call.
- The guard is anything we cannot prove invariant (a `switch`, a non-trivial loop, etc.).
- The call is in a `catch` or `finally` block (those run conditionally). A call in a `try` *body*, however, is still reached unconditionally and is reported.

When in doubt, the analyzer favors *not* reporting to keep false positives low.

## Code that triggers the analyzer

```csharp
public class Example
{    
    // Suspicious: unconditional self-recursion with no base case
    public void ProcessData()
    {
        // Some processing...
        ProcessData(); // ❌ EPC30 - Calls itself unconditionally
    }

    // Suspicious: the guard 'b' is passed unchanged, so this recurses forever once taken
    public void Loop(bool b)
    {
        if (b) Loop(b); // ❌ EPC30 - Invariant guard => infinite recursion
    }
}
```

## Code that does NOT trigger the analyzer

```csharp
public class Example
{
    // OK: a conditional can terminate the recursion before the call.
    public void Foo(int n)
    {
        n++;
        if (n > 10) return;
        Foo(n); // ✅ no warning
    }

    // OK: the argument changes, so the guard is not invariant.
    public void Bar(int n)
    {
        if (n > 0)
        {
            n--;
            Bar(n); // ✅ no warning
        }
    }
}
```

## How to fix

Add proper base cases and ensure recursion terminates:

```csharp
public class Example
{
    // Good: proper recursive method with base case
    public int Factorial(int n) // ✅ Correct
    {
        if (n <= 1) // Base case
            return 1;
        return n * Factorial(n - 1); // Recursive case with progress toward base case
    }
    
    // Good: recursive tree traversal with termination condition
    public void TraverseTree(TreeNode node) // ✅ Correct
    {
        if (node == null) // Base case
            return;
            
        ProcessNode(node);
        
        // Recursive calls on smaller problems
        TraverseTree(node.Left);
        TraverseTree(node.Right);
    }
    
    // Fix the property
    private int _value;
    public int Value
    {
        get { return _value; } // Return the backing field, not the property
        set { _value = value; }
    }
}
```

Convert to iterative approach when appropriate:

```csharp
public class Example
{
    // Convert recursive to iterative to avoid stack overflow for large inputs
    public int FactorialIterative(int n)
    {
        int result = 1;
        for (int i = 2; i <= n; i++)
        {
            result *= i;
        }
        return result;
    }
    
    // Iterative tree traversal using stack
    public void TraverseTreeIterative(TreeNode root)
    {
        if (root == null) return;
        
        var stack = new Stack<TreeNode>();
        stack.Push(root);
        
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            ProcessNode(node);
            
            if (node.Right != null) stack.Push(node.Right);
            if (node.Left != null) stack.Push(node.Left);
        }
    }
}
```

## When recursion is appropriate

- Tree or graph traversal
- Mathematical functions (factorial, Fibonacci)
- Divide-and-conquer algorithms
- When the recursive solution is clearer than iterative

## When to avoid recursion

- Large datasets that might cause stack overflow
- When iterative solution is simpler
- Performance-critical code (recursion has overhead)
- When stack depth is unpredictable
