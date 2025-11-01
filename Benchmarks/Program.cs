using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Columns;

namespace TiXL.Benchmarks
{
    /// <summary>
    /// Comprehensive benchmarks for TiXL core operations
    /// Measures performance of critical paths to detect regressions
    /// </summary>
    
    [SimpleJob(RuntimeMoniker.Net90)]
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.Fastest)]
    [MinColumn, MaxColumn, MeanColumn, MedianColumn]
    public class CoreBenchmarks
    {
        private readonly int[] _largeDataset;
        private readonly Random _random;
        private List<int> _testList;
        
        public CoreBenchmarks()
        {
            _random = new Random(42);
            _largeDataset = Enumerable.Range(0, 10000).Select(i => _random.Next(0, 1000)).ToArray();
            _testList = new List<int>(_largeDataset);
        }
        
        #region Data Processing Benchmarks
        
        [Benchmark]
        public int SumArray_LINQ()
        {
            return _largeDataset.Sum();
        }
        
        [Benchmark]
        public int SumArray_ForLoop()
        {
            int sum = 0;
            for (int i = 0; i < _largeDataset.Length; i++)
            {
                sum += _largeDataset[i];
            }
            return sum;
        }
        
        [Benchmark]
        public int SumArray_Span()
        {
            var span = _largeDataset.AsSpan();
            int sum = 0;
            foreach (var value in span)
            {
                sum += value;
            }
            return sum;
        }
        
        [Benchmark]
        public double AverageArray_LINQ()
        {
            return _largeDataset.Average();
        }
        
        [Benchmark]
        public int[] FilterArray_LINQ()
        {
            return _largeDataset.Where(x => x > 500).ToArray();
        }
        
        [Benchmark]
        public int[] FilterArray_ForLoop()
        {
            var result = new List<int>();
            for (int i = 0; i < _largeDataset.Length; i++)
            {
                if (_largeDataset[i] > 500)
                {
                    result.Add(_largeDataset[i]);
                }
            }
            return result.ToArray();
        }
        
        [Benchmark]
        public int[] SortArray_ArraySort()
        {
            var copy = new int[_largeDataset.Length];
            Array.Copy(_largeDataset, copy, _largeDataset.Length);
            Array.Sort(copy);
            return copy;
        }
        
        [Benchmark]
        public int[] SortArray_LINQ()
        {
            return _largeDataset.OrderBy(x => x).ToArray();
        }
        
        #endregion
        
        #region Mathematical Operations
        
        [Benchmark]
        public double ComputePI_Leibniz()
        {
            double pi = 0.0;
            for (int i = 0; i < 1000000; i++)
            {
                pi += (i % 2 == 0 ? 1.0 : -1.0) / (2.0 * i + 1.0);
            }
            return pi * 4.0;
        }
        
        [Benchmark]
        public double ComputeSquareRoots()
        {
            double sum = 0.0;
            for (int i = 0; i < _largeDataset.Length; i++)
            {
                sum += Math.Sqrt(_largeDataset[i]);
            }
            return sum;
        }
        
        [Benchmark]
        public double ComputeSineWaves()
        {
            double sum = 0.0;
            for (int i = 0; i < 10000; i++)
            {
                sum += Math.Sin(i * 0.01) * Math.Cos(i * 0.01);
            }
            return sum;
        }
        
        [Benchmark]
        public double ComputeMatrixMultiplication()
        {
            double result = 0.0;
            // Simulate matrix operations with nested loops
            for (int i = 0; i < 100; i++)
            {
                for (int j = 0; j < 100; j++)
                {
                    result += Math.Sin(i * j * 0.01) * Math.Cos(i * j * 0.01);
                }
            }
            return result;
        }
        
        #endregion
        
        #region String Operations
        
        [Benchmark]
        public string StringConcatenation_Plus()
        {
            string result = "";
            for (int i = 0; i < 1000; i++)
            {
                result += $"Item_{i}_Value_{_random.Next(1000)}";
            }
            return result;
        }
        
        [Benchmark]
        public string StringConcatenation_StringBuilder()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 1000; i++)
            {
                sb.Append($"Item_{i}_Value_{_random.Next(1000)}");
            }
            return sb.ToString();
        }
        
        [Benchmark]
        public string StringProcessing_Split()
        {
            var testString = string.Join(",", _largeDataset.Select(x => x.ToString()));
            var parts = testString.Split(',');
            return string.Join("_", parts.Where(p => int.Parse(p) > 500));
        }
        
        [Benchmark]
        public string StringProcessing_Regex()
        {
            var text = string.Join(" ", Enumerable.Repeat("TiXL_Benchmark_Test_", 100));
            var pattern = @"\bTest\b";
            return System.Text.RegularExpressions.Regex.Replace(text, pattern, "Performance");
        }
        
        #endregion
        
        #region Memory and Object Operations
        
        [Benchmark]
        public long MemoryAllocation_List()
        {
            var list = new List<int>(_largeDataset.Length);
            for (int i = 0; i < _largeDataset.Length; i++)
            {
                list.Add(_largeDataset[i] * 2);
            }
            return GC.GetTotalMemory(false);
        }
        
        [Benchmark]
        public long MemoryAllocation_Array()
        {
            var array = new int[_largeDataset.Length];
            for (int i = 0; i < _largeDataset.Length; i++)
            {
                array[i] = _largeDataset[i] * 2;
            }
            return GC.GetTotalMemory(false);
        }
        
        [Benchmark]
        public object[] ObjectArray_Creation()
        {
            var array = new object[_largeDataset.Length];
            for (int i = 0; i < _largeDataset.Length; i++)
            {
                array[i] = new { Value = _largeDataset[i], Processed = true };
            }
            return array;
        }
        
        [Benchmark]
        public Dictionary<int, int> DictionaryOperations()
        {
            var dict = new Dictionary<int, int>();
            foreach (var value in _largeDataset)
            {
                dict[value] = value * value;
            }
            
            int sum = 0;
            foreach (var kvp in dict)
            {
                sum += kvp.Value;
            }
            return dict;
        }
        
        #endregion
        
        #region Concurrent Operations
        
        [Benchmark]
        public async Task<int> AsyncOperations_AwaitAll()
        {
            var tasks = Enumerable.Range(0, 100)
                .Select(i => Task.Run(() => {
                    Thread.Sleep(1);
                    return i * i;
                }))
                .ToArray();
                
            var results = await Task.WhenAll(tasks);
            return results.Sum();
        }
        
        [Benchmark]
        public int Parallel_Processing_For()
        {
            var result = 0;
            Parallel.For(0, _largeDataset.Length, i => {
                Interlocked.Add(ref result, _largeDataset[i]);
            });
            return result;
        }
        
        [Benchmark]
        public int Parallel_Processing_ LINQ()
        {
            return _largeDataset.AsParallel().Sum();
        }
        
        [Benchmark]
        public async Task ConcurrentDictionary_Operations()
        {
            var concurrentDict = new Concurrent.ConcurrentDictionary<int, int>();
            
            var tasks = Enumerable.Range(0, 1000)
                .Select(i => Task.Run(() => {
                    concurrentDict[i] = i * i;
                }))
                .ToArray();
                
            await Task.WhenAll(tasks);
            return concurrentDict.Count;
        }
        
        #endregion
        
        #region Graphics-Related Benchmarks
        
        [Benchmark]
        public float ComputeVectorMagnitude()
        {
            float magnitude = 0.0f;
            for (int i = 0; i < _largeDataset.Length; i += 3)
            {
                if (i + 2 < _largeDataset.Length)
                {
                    var x = _largeDataset[i];
                    var y = _largeDataset[i + 1];
                    var z = _largeDataset[i + 2];
                    magnitude += (float)Math.Sqrt(x * x + y * y + z * z);
                }
            }
            return magnitude;
        }
        
        [Benchmark]
        public float[,] MatrixTransformation()
        {
            var matrix = new float[4, 4];
            var angle = (float)(Math.PI / 4.0);
            
            // Initialize rotation matrix
            matrix[0, 0] = (float)Math.Cos(angle);
            matrix[0, 1] = -(float)Math.Sin(angle);
            matrix[1, 0] = (float)Math.Sin(angle);
            matrix[1, 1] = (float)Math.Cos(angle);
            matrix[2, 2] = 1.0f;
            matrix[3, 3] = 1.0f;
            
            // Apply transformation to multiple points
            float sum = 0;
            for (int i = 0; i < 100; i++)
            {
                var x = _random.Next(-100, 100);
                var y = _random.Next(-100, 100);
                var transformedX = matrix[0, 0] * x + matrix[0, 1] * y;
                var transformedY = matrix[1, 0] * x + matrix[1, 1] * y;
                sum += Math.Abs(transformedX) + Math.Abs(transformedY);
            }
            
            return matrix;
        }
        
        [Benchmark]
        public float ColorInterpolation()
        {
            float sum = 0;
            for (int i = 0; i < _largeDataset.Length; i += 3)
            {
                if (i + 2 < _largeDataset.Length)
                {
                    var r = _largeDataset[i] / 255.0f;
                    var g = _largeDataset[i + 1] / 255.0f;
                    var b = _largeDataset[i + 2] / 255.0f;
                    
                    // Linear interpolation between colors
                    var interpolatedR = r * 0.5f + g * 0.3f + b * 0.2f;
                    sum += interpolatedR;
                }
            }
            return sum;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Benchmarks for TiXL operator system performance
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net90)]
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.Fastest)]
    public class OperatorBenchmarks
    {
        private List<TestOperator> _operators;
        
        public OperatorBenchmarks()
        {
            _operators = new List<TestOperator>();
            for (int i = 0; i < 100; i++)
            {
                _operators.Add(new TestOperator($"Operator_{i}"));
            }
        }
        
        [Benchmark]
        public int OperatorEvaluation_Sequential()
        {
            int result = 0;
            foreach (var op in _operators)
            {
                result += op.Evaluate();
            }
            return result;
        }
        
        [Benchmark]
        public int OperatorEvaluation_Parallel()
        {
            int result = 0;
            Parallel.ForEach(_operators, op => {
                Interlocked.Add(ref result, op.Evaluate());
            });
            return result;
        }
        
        [Benchmark]
        public void OperatorChaining()
        {
            var result = 0;
            foreach (var op in _operators)
            {
                result = op.Process(result);
            }
        }
        
        [Benchmark]
        public TestOperator CreateOperator()
        {
            return new TestOperator($"Dynamic_{Guid.NewGuid()}");
        }
        
        [Benchmark]
        public int[] OperatorPipeline_Processing()
        {
            var input = Enumerable.Range(0, 1000).ToArray();
            var result = input;
            
            foreach (var op in _operators)
            {
                result = op.TransformArray(result);
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// Test operator class for benchmarking
    /// </summary>
    public class TestOperator
    {
        private readonly string _name;
        private readonly int _id;
        private static int _nextId = 0;
        
        public TestOperator(string name)
        {
            _name = name;
            _id = Interlocked.Increment(ref _nextId);
        }
        
        public int Evaluate()
        {
            // Simulate operator evaluation
            var result = 0;
            for (int i = 0; i < 100; i++)
            {
                result += (int)(Math.Sin(_id * i * 0.01) * 1000);
            }
            return result;
        }
        
        public int Process(int input)
        {
            // Simulate processing input
            return input + Evaluate() % 1000;
        }
        
        public int[] TransformArray(int[] input)
        {
            var result = new int[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                result[i] = input[i] + Evaluate() % 10;
            }
            return result;
        }
    }
    
    /// <summary>
    /// Main entry point for benchmark execution
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            var config = DefaultConfig.Instance
                .WithOptions(ConfigOptions.JoinSummary)
                .AddLogger(ConsoleLogger.Default)
                .AddColumn(StatisticColumn.Mean, StatisticColumn.StdDev, StatisticColumn.Median)
                .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(Perfolizer.Horology.TimeUnit.Millisecond));
                
            Console.WriteLine("TiXL Performance Benchmarks");
            Console.WriteLine("===========================");
            Console.WriteLine($"BenchmarkDotNet Version: {BenchmarkDotNet.BenchmarkRunner.GetCurrentVersion()}");
            Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
            Console.WriteLine();
            
            // Run all benchmarks
            var summary = BenchmarkRunner.Run(typeof(Program).Assembly, config);
            
            Console.WriteLine();
            Console.WriteLine("Benchmark execution completed!");
            Console.WriteLine($"Results saved to: {summary.ResultsDirectoryPath}");
        }
    }
}
