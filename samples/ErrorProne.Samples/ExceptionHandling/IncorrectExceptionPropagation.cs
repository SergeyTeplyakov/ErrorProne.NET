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
try { throw new Exception(); }
catch (Exception ex)
{
    throw ex;
    //    ~~
    //    Incorrect exception propagation. Use throw; instead
}
        }
    }
}
