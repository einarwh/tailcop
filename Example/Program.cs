using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Example
{
    class Program
    {
        static void Main(string[] args)
        {
            var watch = new Stopwatch();
            watch.Start();

            int x = 10000;
            int y = 10000;
            Console.WriteLine("Add({0}, {1}) = {2}", x, y, Add(x, y));

            //Console.WriteLine("Add1({0}, {1}) = {2}", x, y, Add1(x, y));

            int[] array = new int[10000];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = 100;
            }

            Console.WriteLine("Sum({0} elements) = {1}", array.Length, Sum(array));

            //Console.WriteLine("Sum1({0} elements) = {1}", array.Length, Sum1(array));

            watch.Stop();
            Console.WriteLine("Elapsed ticks: {0}", watch.ElapsedTicks);
        }

        static int Add(int x, int y)
        {
            if (x > 0)
            {
                return Add(x - 1, y + 1);
            }

            return y;
        }

        static int Add1(int x, int y)
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

        static int Sum(int[] array)
        {
            return RecursiveSum(array, 0, 0);
        }

        static int RecursiveSum(int[] array, int index, int acc)
        {
            if (index < array.Length)
            {
                return RecursiveSum(array, index + 1, acc + array[index]);
            }

            return acc;
        }

        static int Sum1(IEnumerable<int> vals)
        {
            return Redux(vals, (x, y) => x + y, 0);
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
