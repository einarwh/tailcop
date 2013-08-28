using System;

namespace Example
{
    class Program
    {
        static void Main(string[] args)
        {
            CallAdd(40404, 50505);
            CallSum(CreateNumbers());
        }

        static int[] CreateNumbers()
        {
            var rand = new Random();
            int[] array = new int[100000];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = rand.Next(1, 100);
            }
            return array;
        }

        static void CallAdd(int x, int y)
        {
            Console.Write("Add({0}, {1})", x, y);
            Console.WriteLine(" = {0}", Add(x, y));
        }

        static int Add(int x, int y)
        {
            if (x > 0)
            {
                return Add(x - 1, y + 1);
            }

            return y;
        }

        static void CallSum(int[] array)
        {
            Console.Write("Sum({0} numbers)", array.Length);
            Console.WriteLine(" = {0}", Sum(array, 0, 0));
        }

        static int Sum(int[] array, int index, int acc)
        {
            if (index < array.Length)
            {
                return Sum(array, index + 1, acc + array[index]);
            }

            return acc;
        }

        

        
    }
}
