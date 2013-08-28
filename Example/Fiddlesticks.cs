using System.Collections.Generic;
using System.Linq;

namespace Example
{
    class Fiddlesticks
    {

        static int Sum(IEnumerable<int> vals)
        {
            return Redux(vals, (x, y) => x + y, 0);
        }

        static int Add(int x, int y)
        {
            return Redux(Repeat(1, x), (a, b) => a + b, y);
        }

        private static IEnumerable<T> Repeat<T>(T val, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return val;
            }
        }

        delegate TR Accumulator<in T, TR>(T x, TR y);

        static TR Redux<T, TR>(IEnumerable<T> inputs, Accumulator<T, TR> accr, TR accd)
        {
            var list = inputs.ToList();
            if (list.Any())
            {
                var head = list.First();
                var tail = list.Skip(1);
                return Redux(tail, accr, accr(head, accd));
            }

            return accd;
        }
    }
}
