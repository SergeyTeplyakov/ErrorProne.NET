using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorProne.Samples.ExceptionHandling
{
    class IncorrectExceptionObservation
    {
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
            catch (Exception exception)
            {
                // ex.Message: 
                // Exception has been thrown by the target of an invocation.
                Console.WriteLine(exception.Message);
                //                          ~~~~~~~
                // Only ex.Message property was observed in exception block!
            }
        }
    }
}
