# EPC30 - Method calls itself recursively

This analyzer detects when a method calls itself recursively, which can lead to stack overflow exceptions if not properly controlled.

## Description

The analyzer warns when a method calls itself recursively. While recursion is sometimes necessary and appropriate, accidental recursion can cause stack overflow errors and infinite loops.

## Code that triggers the analyzer

```csharp
public class Example
{    
    // Suspicious: might be accidental recursion
    public void ProcessData()
    {
        // Some processing...
        ProcessData(); // Calls itself without obvious base case
    }

```

## How to fix

Add proper base cases and ensure recursion terminates:

```csharp
public class Example
{
    // Good: proper recursive method with base case
    public int Factorial(int n)
    {
        if (n <= 1) // Base case
            return 1;
        return n * Factorial(n - 1); // Recursive case with progress toward base case
    }
    
    // Good: recursive tree traversal with termination condition
    public void TraverseTree(TreeNode node)
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
