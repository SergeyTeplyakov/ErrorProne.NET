using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorProne.Samples.ExceptionHandling
{
    class SwallowException
    {
        public void Sample(object arg)
        {
            try { throw new Exception(); }
            catch (Exception e)
            {
                if (e is AggregateException) return;
            } // Exit point '}' swallows an exception
        }
    }
}
