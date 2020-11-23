using System;

namespace CampyDemo
{
    internal class Program
    {
        private static void Main(string[] args)
        {

            var n = 4000;
            var x = new int[n];
            Campy.Parallel.For(n, i => x[i] = i);
            for (var i = 0; i < n; ++i)
                System.Console.WriteLine(x[i]);

            Console.WriteLine("Done!");
        }
    }
}
