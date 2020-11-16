using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace TPLDemo
{
    class Program
    {
        /// <summary>
        /// async sequential running 
        /// </summary>
        /// <param name="sources"></param>
        /// <returns></returns>
        static async Task Runner1(IEnumerable<string> sources)
        {
            var timer = new Stopwatch();
            timer.Start();
            var block = new ActionBlock<string>(async url =>
            {
                try
                {
                    var client = new HttpClient();

                    await client.GetStringAsync(url);
                }
                catch (Exception exp)
                {
                    Console.WriteLine($"Exception: '{exp.Message}' from {url}");
                }
            },
           new ExecutionDataflowBlockOptions
           {
               MaxDegreeOfParallelism = Environment.ProcessorCount
           });

            foreach (var source in sources)
            {
                await block.SendAsync(source);
            }

            block.Complete();

            await block.Completion;

            timer.Stop();

            Console.WriteLine();
            Console.WriteLine("Done!{0:g}", timer.Elapsed);
        }

        /// <summary>
        /// blocking semaphore loop
        /// </summary>
        class Runner2
        {
            private readonly SemaphoreSlim _batcher;
            private readonly IEnumerable<string> _sources;

            public Runner2(IEnumerable<string> sources)
            {
                _sources = sources;
                _batcher = new SemaphoreSlim(sources.Count(), sources.Count());
            }

            public async Task ForAll()
            {
                var timer = new Stopwatch();
                timer.Start();

                foreach (var source in _sources)
                {
                    await Execute(source);
                }

                timer.Stop();
                Console.WriteLine();
                Console.WriteLine("Done!{0:g}", timer.Elapsed);
            }

            private async Task Execute(string url)
            {
                await _batcher.WaitAsync();

                try
                {
                    var client = new HttpClient();

                    await client.GetStringAsync(url);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    _batcher.Release();
                }
            }
        }

        /// <summary>
        /// interlocked parallel loop
        /// </summary>
        /// <param name="sources"></param>
        static void Runner3(IEnumerable<string> sources)
        {
            var locker = new object();
            int count = 0;
            var timer = new Stopwatch();
            timer.Start();

            Parallel.ForEach(sources, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, (url) =>
            {
                Interlocked.Increment(ref count);

                lock (locker)
                {
                    try
                    {
                        var client = new HttpClient();

                        client.GetStringAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    catch (Exception exp)
                    {
                        Console.WriteLine($"Exception: '{exp.Message}' from {url}");
                    }
                }

                Interlocked.Decrement(ref count);
            });

            timer.Stop();
            Console.WriteLine();
            Console.WriteLine("Done!{0:g}", timer.Elapsed);
        }

        /// <summary>
        ///  async parallel running 
        /// </summary>
        /// <param name="sources"></param>
        /// <returns></returns>
        static async Task Runner4(IEnumerable<string> sources)
        {
            var timer = new Stopwatch();
            timer.Start();
            var block = new ActionBlock<string>(async url =>
            {
                try
                {
                    var client = new HttpClient();

                    await client.GetStringAsync(url);
                }
                catch (Exception exp)
                {
                    Console.WriteLine($"Exception: '{exp.Message}' from {url}");
                }
            },
           new ExecutionDataflowBlockOptions
           {
               MaxDegreeOfParallelism = Environment.ProcessorCount
           });

            foreach (var source in sources)
            {
                block.Post(source);
            }
            block.Complete();

            try
            {
                await block.Completion;
            }
            catch (AggregateException exp)
            {
                Console.WriteLine($"Exception: '{exp.Message}'");
            }

            timer.Stop();

            Console.WriteLine();
            Console.WriteLine("Done!{0:g}", timer.Elapsed);
        }

        /// <summary>
        /// parts async loop
        /// </summary>
        /// <param name="sources"></param>
        static void Runner5(IEnumerable<string> sources)
        {
            var timer = new Stopwatch();
            timer.Start();
            var parts = Partitioner.Create(0, sources.Count(), 10).GetDynamicPartitions();

            Parallel.ForEach(parts, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, source =>
             {
                 for (var i = source.Item1; i < source.Item2; i++)
                 {
                     var url = sources.ElementAt(i);

                     try
                     {
                         var client = new HttpClient();

                         client.GetStringAsync(url).Wait();
                     }
                     catch (Exception exp)
                     {
                         Console.WriteLine($"Exception: '{exp.Message}' from {url}");
                     }
                 }
             });

            timer.Stop();

            Console.WriteLine();
            Console.WriteLine("Done!{0:g}", timer.Elapsed);
        }

        /// <summary>
        /// partitonal parallel horizontal loop
        /// </summary>
        static void HorizontalRun()
        {
            var source = Enumerable.Range(0, 10 ^ 8).ToArray();
            var parts = Partitioner.Create(0, source.LongLength);
            var results = new double[source.Length];
            var st = new Stopwatch();
            st.Start();
            //var bag = new ConcurrentBag<double>();

            Parallel.ForEach(parts, async (tuple) =>
            {
                //var block = new TransformBlock<long,double>(i =>
                var block = new ActionBlock<long>(i =>
                {
                    results[i] = source[i] * Math.PI;
                    //bag.Add(results[i]);
                }, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });

                for (var i = tuple.Item1; i < tuple.Item2; i++)
                {
                    block.Post(i);
                }

                block.Complete();

                await block.Completion;
            });

            st.Stop();
            Console.WriteLine($"Done!{st.Elapsed:g}");
        }

        /// <summary>
        /// partitonal parallel vertial loop
        /// </summary>
        static void VerticalRun()
        {
            var source = Enumerable.Range(0, 10 ^ 8).ToArray();
            var parts = Partitioner.Create(0, source.LongLength);
            var results = new double[source.Length];
            var st = new Stopwatch();
            st.Start();

            Parallel.ForEach(parts, (tuple) =>
            {
                var tasks = new Task[tuple.Item2 - tuple.Item1];

                for (var i = tuple.Item1; i < tuple.Item2; i++)
                {
                    tasks[i - tuple.Item1] = Task.Factory.StartNew(o =>
                    {
                        var j = (long)o;

                        results[j] = source[j] * Math.PI;
                    }, i);
                }

                Task.WaitAll(tasks);
            });

            st.Stop();
            Console.WriteLine($"Done!{st.Elapsed:g}");
        }

        static async Task Main(string[] args)
        {
            var sources = Enumerable.Repeat("https://www.google.com/", 10 ^ 6);

            //Console.WriteLine("-------------V#1-------------");
            //await Runner1(sources);
            //Console.WriteLine();

            //Console.WriteLine("-------------V#2-------------");
            //await new Runner2(sources).ForAll();
            //Console.WriteLine();

            //Console.WriteLine("-------------V#3-------------");
            //Runner3(sources);
            //Console.WriteLine();

            //Console.WriteLine("-------------V#4-------------");
            //await Runner4(sources);
            //Console.WriteLine();

            //Console.WriteLine("-------------V#5-------------");
            //Runner5(sources);
            //Console.WriteLine();

            Console.WriteLine("-------------V#6-------------");
            HorizontalRun();
            Console.WriteLine();

            Console.WriteLine("-------------V#7-------------");
            VerticalRun();
            Console.WriteLine();

        }
    }
}
