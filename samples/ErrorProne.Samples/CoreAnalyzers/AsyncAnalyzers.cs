// Comment pragmas to see the diagnostics in the code.
#pragma warning disable EPC16 // Awaiting the result of a null-conditional expression may cause NullReferenceException.

using System.IO;
using System.Threading.Tasks;

//[assembly: DoNotUseConfigureAwaitFalse()]
public class UseConfigureAwaitFalseAttribute : System.Attribute { }

//[assembly: UseConfigureAwaitFalse()]

public class DoNotUseConfigureAwaitFalseAttribute : System.Attribute { }
namespace ErrorProne.Samples.CoreAnalyzers
{
    
    public class AsyncAnalyzers
    {
        public static async Task Sample()
        {
            Stream sample = null;
            // Awaiting the result of a null-conditional expression may cause NullReferenceException.

            await sample?.FlushAsync();
        }

        public static async Task CopyTo(Stream source, Stream destination)
        {
            // ConfigureAwait(false) must be used
            await source.CopyToAsync(destination);

            // ConfigureAwait(false) call is redundant
            await source.CopyToAsync(destination).ConfigureAwait(false);
        }
    }
}

#pragma warning restore EPC16 // Awaiting the result of a null-conditional expression may cause NullReferenceException.