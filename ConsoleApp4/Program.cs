using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace GCOverheadExperiment
{
    class Program
    {
        private static int objectsCreated = 0;
        private static int objectsFinalized = 0;
        private static bool garbageCollectionStarted;
        private static long startTime;
        private static readonly object lockObject = new object();
        private static bool running;
        private static int firstToFinalize = -1;
        
        class BigObject
        {
            public int Id { get; }
            private readonly byte[] data;
            
            public BigObject(int id)
            {
                Id = id;
                data = new byte[1024 * 8];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (byte)(i % 256);
                }
                
                lock (lockObject)
                {
                    objectsCreated++;
                    if (objectsCreated % 100 == 0)
                    {
                        Console.WriteLine($"Objects created: {objectsCreated}");
                    }
                }
            }
            
            ~BigObject()
            {
                lock (lockObject)
                {
                    objectsFinalized++;
                }

                if (!garbageCollectionStarted)
                {
                    garbageCollectionStarted = true;
                    
                }

                if (firstToFinalize == -1)
                {
                    firstToFinalize = Id;
                }
            }
        }

        static void PrintData()
        {
            while (running){ 
                if (objectsFinalized % 100 == 0 && objectsFinalized != 0)
                {
                    Console.WriteLine($"objects finalized: {objectsFinalized}");
                }
                Thread.Sleep(100);
            }
        }

        static void Main(string[] args)
        {
            startTime = Stopwatch.GetTimestamp();
            running = true;
            var p = new Thread(PrintData);
            p.Start();
            Console.WriteLine($"{GC.GetGCMemoryInfo().HeapSizeBytes / 1024 / 1024} MB");
            GC.RegisterForFullGCNotification(10, 10);
            try
            {
                int i = 0;
                while (!garbageCollectionStarted)
                {
                    var obj = new BigObject(++i);
                    obj = null;
                    if (i > 10000)
                    {
                        break;
                    }
                }
                Console.WriteLine($"Objects finalized: {objectsFinalized}, garbage collection started when: {objectsCreated}" +
                                  $"At time {Stopwatch.GetElapsedTime(startTime)} ms");
                Console.WriteLine($"First to finalize: {firstToFinalize}");
                GC.Collect(0, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                Console.WriteLine("");
                
                long collectionStrated = Stopwatch.GetTimestamp();
                
                for (int j = 0; j < 1000; j++)
                {
                    var obj = new BigObject(++i);
                    obj = null;
                    
                    if (j % 50 == 0)
                    {
                        GC.Collect(0, GCCollectionMode.Forced);
                    }
                }
                
                TimeSpan afterCollection = Stopwatch.GetElapsedTime(collectionStrated);
                Console.WriteLine($"\nTime for 1000 obj: {afterCollection.TotalMilliseconds:F2} ms");
            }
            catch (OutOfMemoryException ex)
            {
                Console.WriteLine($"\nException: {ex.Message}");
                Console.WriteLine($"Object count: {objectsCreated}");
            }


            TimeSpan totalTime = Stopwatch.GetElapsedTime(startTime);
            Console.WriteLine("\n");
            Console.WriteLine($"Total time: {totalTime.TotalMilliseconds:F2} мс");
            Console.WriteLine($"Objects created: {objectsCreated}");
            Console.WriteLine($"objects finalized: {objectsFinalized}");
            Console.WriteLine($"Not finalized: {objectsCreated - objectsFinalized}");
            MemInfo();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            running = false;
            p.Join();
            Console.WriteLine("\nAfter gc:");
            MemInfo();
            Console.WriteLine($"objects created: {objectsCreated} objects finalized: {objectsFinalized}");
            Thread.Sleep(1000);
        }
    
        
        private static void MemInfo()
        {
            long totalMemory = GC.GetTotalMemory(false) / (1024 * 1024);
            var gcInfo = GC.GetGCMemoryInfo();
            
            Console.WriteLine($"Total memory: {totalMemory} mb");
            Console.WriteLine($"Total heap size: {gcInfo.HeapSizeBytes / (1024 * 1024)} mb");
            Console.WriteLine($"Gen 0: {GC.CollectionCount(0)}");
            Console.WriteLine($"Gen 1: {GC.CollectionCount(1)}");
            Console.WriteLine($"Gen 2: {GC.CollectionCount(2)}");
            
            for (int gen = 0; gen <= 2; gen++)
            {
                var genInfo = gcInfo.GenerationInfo[gen];
                Console.WriteLine($"  Gen {gen}: Size={genInfo.SizeBeforeBytes / 1024 / 1024} MB, " +
                                  $"Frag={genInfo.FragmentationBeforeBytes / 1024 / 1024} MB");
            }
        }
    }
}