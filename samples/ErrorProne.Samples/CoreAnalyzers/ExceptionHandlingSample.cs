using System;

namespace ErrorProne.Samples.CoreAnalyzers
{
    public class ExceptionHandlingSample
    {

        public static void ExceptionPropagation()
        {
            try
            {
                Console.WriteLine();
            }
            catch (Exception e)
            {
                // Suspicious exception handling: only e.Message is observed in exception block
                Console.WriteLine(e.Message);
            }

            try
            {
                Console.WriteLine();
            }
            catch (Exception e)
            {
                // Exit point 'return' swallows an unobserved exception
                return;
            }

            try
            {
                Console.WriteLine();
            }
            catch (Exception e)
            {
                // Incorrect exception propagation: use 'throw' instead
                throw e;
            }
        }
    }
}