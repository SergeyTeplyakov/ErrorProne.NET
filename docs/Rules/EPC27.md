# EPC27 - Avoid async void methods

This analyzer detects `async void` methods, which should be avoided except for event handlers.

## Description

The analyzer warns when methods are declared as `async void`. These methods are dangerous because exceptions thrown in them cannot be caught by the caller and will crash the application. They should only be used for event handlers.

## Code that triggers the analyzer

```csharp
public class Example
{
    // Bad: async void method
    public async void ProcessData()
    {
        await SomeAsyncOperation();
        // If this throws, it will crash the app
    }
    
    // Also bad: async void in other contexts
    private async void Helper()
    {
        await DoWorkAsync();
    }
}
```

## How to fix

Use `async Task` instead:

```csharp
public class Example
{
    // Good: async Task method
    public async Task ProcessDataAsync()
    {
        await SomeAsyncOperation();
        // Exceptions can be caught by caller
    }
    
    // Good: async Task for helper methods
    private async Task HelperAsync()
    {
        await DoWorkAsync();
    }
    
    // Usage example
    public async Task CallerAsync()
    {
        try
        {
            await ProcessDataAsync();
        }
        catch (Exception ex)
        {
            // Can catch exceptions properly
            LogError(ex);
        }
    }
}
```

The only acceptable use of `async void` is for event handlers:

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    // Acceptable: event handler must be async void
    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ProcessClickAsync();
        }
        catch (Exception ex)
        {
            // Always wrap event handlers in try-catch
            MessageBox.Show($"Error: {ex.Message}");
        }
    }
    
    private async Task ProcessClickAsync()
    {
        // Actual work in async Task method
        await SomeAsyncOperation();
    }
}
```

## Best practices

1. Use `async Task` for all async methods except event handlers
2. Always wrap async void event handlers in try-catch blocks
3. Prefer extracting logic from event handlers into async Task methods
4. Use `async Task<T>` when the method needs to return a value
