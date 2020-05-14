using System;

namespace ErrorProne.Samples.StructAnalyzers
{
    public static class TestReadOnlyMemory
    {
        public static void WithReadOnlySequence(in ReadOnlyMemory<byte> r)
        {

        }
    }
}