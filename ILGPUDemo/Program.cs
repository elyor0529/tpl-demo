using System;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUDemo
{
    internal class Program
    {
        static void MyKernel(
            Index1 index,             
            ArrayView<int> dataView,   
            int constant)               
        {
            dataView[index] = index + constant;
        }

        private static void Main(string[] args)
        {
            using var context = new Context();

            foreach (var acceleratorId in Accelerator.Accelerators)
            {
                using var accelerator = Accelerator.Create(context, acceleratorId);
                Console.WriteLine($"Performing operations on {accelerator}");
    
                var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1, ArrayView<int>, int>(MyKernel);
                using var buffer = accelerator.Allocate<int>(1024);
                        
                kernel(buffer.Length, buffer.View, 42);
                accelerator.Synchronize();

                var data = buffer.GetAsArray();
                for (int i = 0, e = data.Length; i < e; ++i)
                {
                    if (data[i] != 42 + i)
                        Console.WriteLine($"Error at element location {i}: {data[i]} found");
                }
            }

            Console.WriteLine("Done!");
        }
    }
}
