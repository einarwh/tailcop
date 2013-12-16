using System;

namespace Tailcop
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Specify a DLL to rewrite.");
                Console.ReadKey();
                return;
            }

            var copper = args.Length == 1 ? new Tailcopper() : new Tailcopper(args[1]);
            copper.TamperWith(args[0]);
        }
    }
}
