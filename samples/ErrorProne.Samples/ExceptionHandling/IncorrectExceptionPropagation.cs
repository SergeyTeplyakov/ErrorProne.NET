using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorProne.Samples.ExceptionHandling
{
    class IncorrectExceptionPropagation
    {
        public void Sample()
        {
            // Incorrect exception propagation
            try { throw new Exception(); }
            catch (Exception e)
            { throw e; }
        }
    }
}
