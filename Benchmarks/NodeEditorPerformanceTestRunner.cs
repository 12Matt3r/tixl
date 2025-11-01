using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace TiXL.Benchmarks
{
    /// <summary>
    /// Simple performance test runner for node editor optimizations
    /// Can run without BenchmarkDotNet for quick validation
    /// </summary>
    public class NodeEditorPerformanceTestRunner
    {
        private readonly Stopwatch _stopwatch = new();
        
        public static void Main(string[] args)
        {
            Console.WriteLine("TiXL Node Editor Performance Test Runner");
            Console.WriteLine("==========================================");
            Console.WriteLine();
            
            var runner = new NodeEditorPerformanceTestRunner();
            
            // Run performance tests for different graph sizes
            var graphSizes = new[] { 100, 500, 1000, 5000 };
            
            foreach (var size in graphSizes)
            {
                Console.WriteLine($"Testing with {size} nodes...");
                runner.RunComprehensiveTests(size);
                Console.WriteLine();
            }
            
            Console.WriteLine("Performance testing completed. Press any key to exit.");
            Console.ReadKey();
        }
        
        public void RunComprehensiveTests(int nodeCount)
        {
            var results = new List<BenchmarkResult>();
            
            // Test 1: Full Graph Evaluation
            Console.WriteLine("  Testing full graph evaluation...");
            results.Add(TestFullGraphEvaluation(nodeCount));
            
            // Test 2: Incremental Evaluation
            Console.WriteLine("  Testing incremental evaluation...");
            results.Add(TestIncrementalEvaluation(nodeCount));
            
            // Test 3: UI Rendering
            Console.WriteLine("  Testing UI rendering...");
            results.Add(TestUIRendering(nodeCount));
            
            // Test 4: Connection Validation
            Console.WriteLine("  Testing connection validation...");
            results.Add(TestConnectionValidation(nodeCount));
            
            // Test 5: Parameter Change Detection
            Console.WriteLine("  Testing parameter change detection...");
            results.Add(TestParameterChangeDetection(nodeCount));
            
            // Print results
            PrintResults(results, nodeCount);
        }
        
        private BenchmarkResult TestFullGraphEvaluation(int nodeCount)
        {
            var originalGraph = CreateOriginalGraph(nodeCount);
            var optimizedGraph = CreateOptimizedGraph(nodeCount);
            
            // Original implementation
            _stopwatch.Restart();
            var originalResult = originalGraph.EvaluateAll();
            _stopwatch.Stop();
            var originalTime = _stopwatch.Elapsed.TotalMilliseconds;
            
            // Optimized implementation
            _stopwatch.Restart();
            var optimizedResult = optimizedGraph.EvaluateAll();
            _stopwatch.Stop();
            var optimizedTime = _stopwatch.Elapsed.TotalMilliseconds;
            
            return new BenchmarkResult
            {
                NodeCount = nodeCount,
                TestName = "Full Graph Evaluation",
                OriginalTime = originalTime,
                OptimizedTime = optimizedTime,
                Improvement = originalTime > 0 ? (originalTime - optimizedTime) / originalTime : 0,
                OriginalEvaluations = originalResult.NodeEvaluations,
                OptimizedEvaluations = optimizedResult.NodeEvaluations
            };
        }
        
        private BenchmarkResult TestIncrementalEvaluation(int nodeCount)
        {
            var originalGraph = CreateOriginalGraph(nodeCount);
            var optimizedGraph = CreateOptimizedGraph(nodeCount);
            
            // Simulate changes to 1% of nodes
            var changedNodeCount = Math.Max(1, nodeCount / 100);
            var changedNodes = Enumerable.Range(0, changedNodeCount)
                .Select(i => $"Node_{i}")
                .ToList();
            
            // Original: Full re-evaluation
            _stopwatch.Restart();
            foreach (var nodeId in changedNodes)
            {
                // Simulate parameter change
                var node = originalGraph.Nodes.First(n => n.Id == nodeId);
                node.UpdateParameter("value", new Random().Next());
            }
            var originalResult = originalGraph.EvaluateAll();
            _stopwatch.Stop();
            var originalTime = _stopwatch.Elapsed.TotalMilliseconds;
            
            // Optimized: Only evaluate affected nodes
            _stopwatch.Restart();
            foreach (var nodeId in changedNodes)
            {
                var node = optimizedGraph.Nodes.First(n => n.Id == nodeId);
                node.UpdateParameter("value", new Random().Next());
            }
            var optimizedResult = optimizedGraph.EvaluateIncremental(
                changedNodes.Select(id => optimizedGraph.Nodes.First(n => n.Id == id)).ToList()
            );
            _stopwatch.Stop();
            var optimizedTime = _stopwatch.Elapsed.TotalMilliseconds;
            
            return new BenchmarkResult
            {
                NodeCount = nodeCount,
                TestName = "Incremental Evaluation",
                OriginalTime = originalTime,
                OptimizedTime = optimizedTime,
                Improvement = originalTime > 0 ? (originalTime - optimizedTime) / originalTime : 0,
                OriginalEvaluations = originalResult.NodeEvaluations,
                OptimizedEvaluations = optimizedResult.AffectedNodeCount
            };
        }
        
        private BenchmarkResult TestUIRendering(int nodeCount)
        {
            var viewport = CreateTestViewport();
            var originalGraph = CreateOriginalGraph(nodeCount);
            var optimizedGraph = CreateOptimizedGraph(nodeCount);
            
            // Original: Render all nodes
            _stopwatch.Restart();
            var originalResult = originalGraph.RenderAll(viewport);
            _stopwatch.Stop();
            var originalTime = _stopwatch.Elapsed.TotalMilliseconds;
            
            // Optimized: Virtualized rendering
            _stopwatch.Restart();
            var optimizedResult = optimizedGraph.RenderOptimized(viewport);
            _stopwatch.Stop();
            var optimizedTime = _stopwatch.Elapsed.TotalMilliseconds;
            
            return new BenchmarkResult
            {
                NodeCount = nodeCount,
                TestName = "UI Rendering",
                OriginalTime = originalTime,
                OptimizedTime = optimizedTime,
                Improvement = originalTime > 0 ? (originalTime - optimizedTime) / originalTime : 0,
                OriginalEvaluations = originalResult.RenderedNodes,
                OptimizedEvaluations = optimizedResult.RenderedNodes
            };
        }
        
        private BenchmarkResult TestConnectionValidation(int nodeCount)
        {
            var connectionCount = Math.Max(1, nodeCount / 10);
            var connections = CreateRandomConnections(connectionCount);
            
            var originalValidator = new OriginalConnectionValidator();
            var optimizedValidator = new OptimizedConnectionValidator();
            
            // Original: Validate all connections
            _stopwatch.Restart();
            var originalResults = new List<bool>();
            foreach (var connection in connections)
            {
                var isValid = originalValidator.ValidateConnection(connection);
                originalResults.Add(isValid);
            }
            _stopwatch.Stop();
            var originalTime = _stopwatch.Elapsed.TotalMilliseconds;
            
            // Optimized: Batch validate with caching
            _stopwatch.Restart();
            var optimizedResults = optimizedValidator.ValidateConnectionsBatch(connections);
            _stopwatch.Stop();
            var optimizedTime = _stopwatch.Elapsed.TotalMilliseconds;
            
            return new BenchmarkResult
            {
                NodeCount = nodeCount,
                TestName = "Connection Validation",
                OriginalTime = originalTime,
                OptimizedTime = optimizedTime,
                Improvement = originalTime > 0 ? (originalTime - optimizedTime) / originalTime : 0,
                OriginalEvaluations = connections.Count,
                OptimizedEvaluations = optimizedResults.Count
            };
        }
        
        private BenchmarkResult TestParameterChangeDetection(int nodeCount)
        {
            var parameterCount = nodeCount * 2; // 2 params per node
            var parameters = CreateRandomParameters(parameterCount);
            
            var originalDetector = new OriginalParameterChangeDetector();
            var optimizedDetector = new OptimizedParameterChangeDetector();
            
            // Original: Check all parameters
            _stopwatch.Restart();
            var originalChanges = new List<bool>();
            foreach (var parameter in parameters)
            {
                var hasChanged = originalDetector.CheckParameterChange(parameter.Key, parameter.Value);
                originalChanges.Add(hasChanged);
            }
            _stopwatch.Stop();
            var originalTime = _stopwatch.Elapsed.TotalMilliseconds;
            
            // Optimized: Batch check with hash-based detection
            _stopwatch.Restart();
            var optimizedResults = optimizedDetector.CheckParameterChangesBatch(parameters);
            _stopwatch.Stop();
            var optimizedTime = _stopwatch.Elapsed.TotalMilliseconds;
            
            var changedCount = optimizedResults.Count(r => r.HasChanged);
            
            return new BenchmarkResult
            {
                NodeCount = nodeCount,
                TestName = "Parameter Change Detection",
                OriginalTime = originalTime,
                OptimizedTime = optimizedTime,
                Improvement = originalTime > 0 ? (originalTime - optimizedTime) / originalTime : 0,
                OriginalEvaluations = parameters.Count,
                OptimizedEvaluations = changedCount
            };
        }
        
        private void PrintResults(List<BenchmarkResult> results, int nodeCount)
        {
            Console.WriteLine($"\nResults for {nodeCount} nodes:");
            Console.WriteLine("=" + new string('=', 80));
            Console.WriteLine($"{"Test Name",-30} {"Original",-12} {"Optimized",-12} {"Improvement",-12} {"Evaluations"}");
            Console.WriteLine(new string('-', 80));
            
            foreach (var result in results)
            {
                Console.WriteLine($"{result.TestName,-30} {result.OriginalTime,8:F2}ms {result.OptimizedTime,8:F2}ms {result.Improvement,8:P1} {result.OriginalEvaluations} -> {result.OptimizedEvaluations}");
            }
            
            // Calculate averages
            var avgImprovement = results.Average(r => r.Improvement);
            var totalOriginalTime = results.Sum(r => r.OriginalTime);
            var totalOptimizedTime = results.Sum(r => r.OptimizedTime);
            
            Console.WriteLine(new string('-', 80));
            Console.WriteLine($"{"AVERAGE",-30} {totalOriginalTime,8:F2}ms {totalOptimizedTime,8:F2}ms {avgImprovement,8:P1}");
            Console.WriteLine();
        }
        
        private NodeGraph CreateOriginalGraph(int nodeCount)
        {
            var graph = new NodeGraph();
            
            for (int i = 0; i < nodeCount; i++)
            {
                var node = new OriginalNode($"Node_{i}");
                graph.AddNode(node);
                
                // Create some connections
                if (i > 0 && i % 10 == 0)
                {
                    var parentNode = graph.Nodes[i - new Random().Next(1, 10)];
                    graph.ConnectNodes(parentNode, node);
                }
            }
            
            return graph;
        }
        
        private OptimizedNodeGraph CreateOptimizedGraph(int nodeCount)
        {
            var graph = new OptimizedNodeGraph();
            
            for (int i = 0; i < nodeCount; i++)
            {
                var node = new OptimizedNode($"Node_{i}");
                graph.AddNode(node);
                
                if (i > 0 && i % 10 == 0)
                {
                    var parentNode = graph.Nodes[i - new Random().Next(1, 10)];
                    graph.ConnectNodes(parentNode, node);
                }
            }
            
            return graph;
        }
        
        private Viewport CreateTestViewport()
        {
            return new Viewport
            {
                Bounds = new Rectangle { X = 0, Y = 0, Width = 1920, Height = 1080 },
                Zoom = 1.0,
                Scroll = new Point { X = 0, Y = 0 }
            };
        }
        
        private List<Connection> CreateRandomConnections(int count)
        {
            var connections = new List<Connection>();
            var random = new Random();
            
            for (int i = 0; i < count; i++)
            {
                connections.Add(new Connection
                {
                    FromNode = new NodeId($"Node_{random.Next(1000)}"),
                    ToNode = new NodeId($"Node_{random.Next(1000)}"),
                    FromPort = new PortId($"Out_{random.Next(5)}"),
                    ToPort = new PortId($"In_{random.Next(5)}")
                });
            }
            
            return connections;
        }
        
        private Dictionary<ParameterKey, object> CreateRandomParameters(int count)
        {
            var parameters = new Dictionary<ParameterKey, object>();
            var random = new Random();
            
            for (int i = 0; i < count; i++)
            {
                var nodeId = new NodeId($"Node_{random.Next(1000)}");
                var parameterId = new ParameterId($"Param_{random.Next(10)}");
                var parameterKey = new ParameterKey(nodeId, parameterId);
                var value = random.NextDouble();
                
                parameters[parameterKey] = value;
            }
            
            return parameters;
        }
    }
    
    // Simple implementations for testing
    public class SimpleNodeGraph
    {
        public List<SimpleNode> Nodes { get; set; } = new();
        
        public void AddNode(SimpleNode node) => Nodes.Add(node);
        public void ConnectNodes(SimpleNode parent, SimpleNode child) { }
        
        public void EvaluateAll()
        {
            foreach (var node in Nodes)
            {
                node.Evaluate();
            }
        }
        
        public void RenderAll(Viewport viewport)
        {
            foreach (var node in Nodes)
            {
                // Simulate rendering work
                Thread.Sleep(1);
            }
        }
    }
    
    public class SimpleNode
    {
        public string Id { get; }
        
        public SimpleNode(string id)
        {
            Id = id;
        }
        
        public void Evaluate()
        {
            // Simulate evaluation work
            var result = 0.0;
            for (int i = 0; i < 1000; i++)
            {
                result += Math.Sin(i) * Math.Cos(i);
            }
        }
        
        public void UpdateParameter(string name, object value)
        {
            // Simulate parameter update
        }
    }
    
    // Additional supporting classes from the main implementation
    public class NodeId
    {
        public string Id { get; }
        
        public NodeId(string id)
        {
            Id = id;
        }
    }
    
    public class ParameterId
    {
        public string Id { get; }
        
        public ParameterId(string id)
        {
            Id = id;
        }
    }
    
    public class ParameterKey
    {
        public NodeId NodeId { get; }
        public ParameterId ParameterId { get; }
        
        public ParameterKey(NodeId nodeId, ParameterId parameterId)
        {
            NodeId = nodeId;
            ParameterId = parameterId;
        }
        
        public override bool Equals(object obj)
        {
            return obj is ParameterKey other && 
                   NodeId.Id == other.NodeId.Id && 
                   ParameterId.Id == other.ParameterId.Id;
        }
        
        public override int GetHashCode()
        {
            return NodeId.Id.GetHashCode() ^ ParameterId.Id.GetHashCode();
        }
    }
    
    public class PortId
    {
        public string Id { get; }
        
        public PortId(string id)
        {
            Id = id;
        }
    }
}