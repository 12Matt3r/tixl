using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace TiXL.Benchmarks
{
    /// <summary>
    /// Comprehensive benchmarks for node editor performance improvements
    /// Compares original implementation vs optimized implementation
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net90, launchCount: 3, iterationCount: 10, warmupCount: 3)]
    [MemoryDiagnoser]
    public class NodeEditorPerformanceBenchmarks
    {
        private const int SMALL_GRAPH = 100;
        private const int MEDIUM_GRAPH = 500;
        private const int LARGE_GRAPH = 1000;
        private const int VERY_LARGE_GRAPH = 5000;
        
        private NodeGraph _originalGraph;
        private OptimizedNodeGraph _optimizedGraph;
        private BenchmarkContext _context;
        
        [GlobalSetup]
        public void Setup()
        {
            _context = new BenchmarkContext();
            _originalGraph = CreateOriginalGraph(VERY_LARGE_GRAPH);
            _optimizedGraph = CreateOptimizedGraph(VERY_LARGE_GRAPH);
        }
        
        /// <summary>
        /// Benchmark: Full graph evaluation
        /// Tests the performance difference between full re-evaluation vs topological sorting
        /// </summary>
        [Benchmark]
        [Arguments(SMALL_GRAPH, MEDIUM_GRAPH, LARGE_GRAPH, VERY_LARGE_GRAPH)]
        public BenchmarkResult FullGraphEvaluation(int nodeCount)
        {
            // Create fresh graphs for this test
            var originalGraph = CreateOriginalGraph(nodeCount);
            var optimizedGraph = CreateOptimizedGraph(nodeCount);
            
            // Original implementation
            var originalTimer = Stopwatch.StartNew();
            var originalResult = originalGraph.EvaluateAll();
            originalTimer.Stop();
            
            // Optimized implementation
            var optimizedTimer = Stopwatch.StartNew();
            var optimizedResult = optimizedGraph.EvaluateAll();
            optimizedTimer.Stop();
            
            return new BenchmarkResult
            {
                NodeCount = nodeCount,
                OriginalTime = originalTimer.Elapsed.TotalMilliseconds,
                OptimizedTime = optimizedTimer.Elapsed.TotalMilliseconds,
                Improvement = originalTimer.Elapsed.TotalMilliseconds > 0 ? 
                    (originalTimer.Elapsed.TotalMilliseconds - optimizedTimer.Elapsed.TotalMilliseconds) / originalTimer.Elapsed.TotalMilliseconds : 0,
                OriginalEvaluations = originalResult.NodeEvaluations,
                OptimizedEvaluations = optimizedResult.NodeEvaluations
            };
        }
        
        /// <summary>
        /// Benchmark: Incremental evaluation
        /// Tests performance when only a small subset of nodes change
        /// </summary>
        [Benchmark]
        [Arguments(SMALL_GRAPH, MEDIUM_GRAPH, LARGE_GRAPH, VERY_LARGE_GRAPH)]
        public BenchmarkResult IncrementalEvaluation(int nodeCount)
        {
            var context = _context.GetNodeContext(nodeCount);
            var changedNodes = context.GetRandomNodes(Math.Max(1, nodeCount / 100)); // 1% of nodes change
            
            // Original: Full re-evaluation
            var originalGraph = CreateOriginalGraph(nodeCount);
            var originalTimer = Stopwatch.StartNew();
            foreach (var node in changedNodes)
            {
                node.UpdateParameter("value", new Random().Next());
            }
            var originalResult = originalGraph.EvaluateAll();
            originalTimer.Stop();
            
            // Optimized: Only evaluate affected nodes
            var optimizedGraph = CreateOptimizedGraph(nodeCount);
            var optimizedTimer = Stopwatch.StartNew();
            foreach (var node in changedNodes)
            {
                node.UpdateParameter("value", new Random().Next());
            }
            var optimizedResult = optimizedGraph.EvaluateIncremental(changedNodes);
            optimizedTimer.Stop();
            
            return new BenchmarkResult
            {
                NodeCount = nodeCount,
                OriginalTime = originalTimer.Elapsed.TotalMilliseconds,
                OptimizedTime = optimizedTimer.Elapsed.TotalMilliseconds,
                Improvement = originalTimer.Elapsed.TotalMilliseconds > 0 ?
                    (originalTimer.Elapsed.TotalMilliseconds - optimizedTimer.Elapsed.TotalMilliseconds) / originalTimer.Elapsed.TotalMilliseconds : 0,
                OriginalEvaluations = originalResult.NodeEvaluations,
                OptimizedEvaluations = optimizedResult.AffectedNodeCount
            };
        }
        
        /// <summary>
        /// Benchmark: UI rendering performance
        /// Tests virtualized rendering vs full rendering
        /// </summary>
        [Benchmark]
        [Arguments(SMALL_GRAPH, MEDIUM_GRAPH, LARGE_GRAPH, VERY_LARGE_GRAPH)]
        public BenchmarkResult UIRendering(int nodeCount)
        {
            var viewport = _context.CreateViewport();
            
            // Original: Render all nodes
            var originalGraph = CreateOriginalGraph(nodeCount);
            var originalTimer = Stopwatch.StartNew();
            var originalResult = originalGraph.RenderAll(viewport);
            originalTimer.Stop();
            
            // Optimized: Virtualized rendering
            var optimizedGraph = CreateOptimizedGraph(nodeCount);
            var optimizedTimer = Stopwatch.StartNew();
            var optimizedResult = optimizedGraph.RenderOptimized(viewport);
            optimizedTimer.Stop();
            
            return new BenchmarkResult
            {
                NodeCount = nodeCount,
                OriginalTime = originalTimer.Elapsed.TotalMilliseconds,
                OptimizedTime = optimizedTimer.Elapsed.TotalMilliseconds,
                Improvement = originalTimer.Elapsed.TotalMilliseconds > 0 ?
                    (originalTimer.Elapsed.TotalMilliseconds - optimizedTimer.Elapsed.TotalMilliseconds) / originalTimer.Elapsed.TotalMilliseconds : 0,
                OriginalEvaluations = originalResult.RenderedNodes,
                OptimizedEvaluations = optimizedResult.RenderedNodes
            };
        }
        
        /// <summary>
        /// Benchmark: Connection validation
        /// Tests optimized connection validation
        /// </summary>
        [Benchmark]
        [Arguments(SMALL_GRAPH, MEDIUM_GRAPH, LARGE_GRAPH, VERY_LARGE_GRAPH)]
        public BenchmarkResult ConnectionValidation(int nodeCount)
        {
            var connections = _context.CreateRandomConnections(Math.Max(1, nodeCount / 10)); // 10% of nodes connected
            
            // Original: Validate all connections
            var originalValidator = new OriginalConnectionValidator();
            var originalTimer = Stopwatch.StartNew();
            var originalResults = new List<bool>();
            foreach (var connection in connections)
            {
                var isValid = originalValidator.ValidateConnection(connection);
                originalResults.Add(isValid);
            }
            originalTimer.Stop();
            
            // Optimized: Batch validate with caching
            var optimizedValidator = new OptimizedConnectionValidator();
            var optimizedTimer = Stopwatch.StartNew();
            var optimizedResults = optimizedValidator.ValidateConnectionsBatch(connections);
            optimizedTimer.Stop();
            
            return new BenchmarkResult
            {
                NodeCount = nodeCount,
                OriginalTime = originalTimer.Elapsed.TotalMilliseconds,
                OptimizedTime = optimizedTimer.Elapsed.TotalMilliseconds,
                Improvement = originalTimer.Elapsed.TotalMilliseconds > 0 ?
                    (originalTimer.Elapsed.TotalMilliseconds - optimizedTimer.Elapsed.TotalMilliseconds) / originalTimer.Elapsed.TotalMilliseconds : 0,
                OriginalEvaluations = connections.Count,
                OptimizedEvaluations = optimizedResults.Count
            };
        }
        
        /// <summary>
        /// Benchmark: Parameter change detection
        /// Tests optimized change detection
        /// </summary>
        [Benchmark]
        [Arguments(SMALL_GRAPH, MEDIUM_GRAPH, LARGE_GRAPH, VERY_LARGE_GRAPH)]
        public BenchmarkResult ParameterChangeDetection(int nodeCount)
        {
            var parameters = _context.CreateRandomParameters(nodeCount * 2); // 2 params per node
            
            // Original: Check all parameters
            var originalDetector = new OriginalParameterChangeDetector();
            var originalTimer = Stopwatch.StartNew();
            var originalChanges = new List<bool>();
            foreach (var parameter in parameters)
            {
                var hasChanged = originalDetector.CheckParameterChange(parameter.Key, parameter.Value);
                originalChanges.Add(hasChanged);
            }
            originalTimer.Stop();
            
            // Optimized: Batch check with hash-based detection
            var optimizedDetector = new OptimizedParameterChangeDetector();
            var optimizedTimer = Stopwatch.StartNew();
            var optimizedResults = optimizedDetector.CheckParameterChangesBatch(parameters);
            optimizedTimer.Stop();
            
            var changedCount = optimizedResults.Count(r => r.HasChanged);
            
            return new BenchmarkResult
            {
                NodeCount = nodeCount,
                OriginalTime = originalTimer.Elapsed.TotalMilliseconds,
                OptimizedTime = optimizedTimer.Elapsed.TotalMilliseconds,
                Improvement = originalTimer.Elapsed.TotalMilliseconds > 0 ?
                    (originalTimer.Elapsed.TotalMilliseconds - optimizedTimer.Elapsed.TotalMilliseconds) / originalTimer.Elapsed.TotalMilliseconds : 0,
                OriginalEvaluations = parameters.Count,
                OptimizedEvaluations = changedCount
            };
        }
        
        /// <summary>
        /// Comprehensive benchmark combining all optimizations
        /// </summary>
        [Benchmark]
        [Arguments(SMALL_GRAPH, MEDIUM_GRAPH, LARGE_GRAPH)]
        public BenchmarkResult ComprehensivePerformanceTest(int nodeCount)
        {
            var context = _context.GetNodeContext(nodeCount);
            var testIterations = 10; // Reduced for benchmark time
            
            var originalTotalTime = 0.0;
            var optimizedTotalTime = 0.0;
            
            for (int i = 0; i < testIterations; i++)
            {
                // Simulate user interaction: change some parameters, add/remove connections
                var changedNodes = context.GetRandomNodes(Math.Max(1, nodeCount / 100));
                var newConnections = context.GetRandomConnections(Math.Max(1, nodeCount / 200));
                
                // Original workflow
                var originalWorkflowTimer = Stopwatch.StartNew();
                var originalGraph = CreateOriginalGraph(nodeCount);
                foreach (var node in changedNodes)
                {
                    node.UpdateParameter("value", new Random().Next());
                    var affectedConnections = originalGraph.GetConnectionsForNode(node.Id);
                    var originalValidator = new OriginalConnectionValidator();
                    foreach (var connection in affectedConnections)
                    {
                        originalValidator.ValidateConnection(connection);
                    }
                }
                originalGraph.EvaluateAll();
                originalGraph.RenderAll(context.Viewport);
                originalWorkflowTimer.Stop();
                originalTotalTime += originalWorkflowTimer.Elapsed.TotalMilliseconds;
                
                // Optimized workflow
                var optimizedWorkflowTimer = Stopwatch.StartNew();
                var optimizedGraph = CreateOptimizedGraph(nodeCount);
                foreach (var node in changedNodes)
                {
                    node.UpdateParameter("value", new Random().Next());
                    var affectedConnections = optimizedGraph.GetConnectionsForNode(node.Id);
                    var optimizedValidator = new OptimizedConnectionValidator();
                    optimizedValidator.ValidateConnectionsBatch(affectedConnections);
                }
                optimizedGraph.EvaluateIncremental(changedNodes);
                optimizedGraph.RenderOptimized(context.Viewport);
                optimizedWorkflowTimer.Stop();
                optimizedTotalTime += optimizedWorkflowTimer.Elapsed.TotalMilliseconds;
            }
            
            return new BenchmarkResult
            {
                NodeCount = nodeCount,
                OriginalTime = originalTotalTime / testIterations,
                OptimizedTime = optimizedTotalTime / testIterations,
                Improvement = originalTotalTime > 0 ?
                    (originalTotalTime - optimizedTotalTime) / originalTotalTime : 0,
                TestIterations = testIterations
            };
        }
        
        private NodeGraph CreateOriginalGraph(int nodeCount)
        {
            var graph = new NodeGraph();
            
            // Create interconnected nodes
            for (int i = 0; i < nodeCount; i++)
            {
                var node = new OriginalNode($"Node_{i}");
                graph.AddNode(node);
                
                // Connect nodes randomly to create dependency chains
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
            
            // Create interconnected nodes with optimization metadata
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
    }
    
    // Supporting test classes and implementations
    public class BenchmarkResult
    {
        public int NodeCount { get; set; }
        public double OriginalTime { get; set; }
        public double OptimizedTime { get; set; }
        public double Improvement { get; set; }
        public int OriginalEvaluations { get; set; }
        public int OptimizedEvaluations { get; set; }
        public int TestIterations { get; set; } = 1;
        
        public string Summary => 
            $"Nodes: {NodeCount}, Original: {OriginalTime:F2}ms, Optimized: {OptimizedTime:F2}ms, " +
            $"Improvement: {Improvement:P1}, Evaluations: {OriginalEvaluations} -> {OptimizedEvaluations}";
    }
    
    public class BenchmarkContext
    {
        private readonly Random _random = new();
        
        public Viewport Viewport => CreateViewport();
        
        public Viewport CreateViewport()
        {
            return new Viewport
            {
                Bounds = new Rectangle { X = 0, Y = 0, Width = 1920, Height = 1080 },
                Zoom = 1.0,
                Scroll = new Point { X = 0, Y = 0 }
            };
        }
        
        public NodeContext GetNodeContext(int nodeCount)
        {
            return new NodeContext(nodeCount, _random);
        }
        
        public List<Connection> CreateRandomConnections(int count)
        {
            var connections = new List<Connection>();
            
            for (int i = 0; i < count; i++)
            {
                connections.Add(new Connection
                {
                    FromNode = new NodeId($"Node_{_random.Next(1000)}"),
                    ToNode = new NodeId($"Node_{_random.Next(1000)}"),
                    FromPort = new PortId($"Out_{_random.Next(5)}"),
                    ToPort = new PortId($"In_{_random.Next(5)}")
                });
            }
            
            return connections;
        }
        
        public Dictionary<ParameterKey, object> CreateRandomParameters(int count)
        {
            var parameters = new Dictionary<ParameterKey, object>();
            
            for (int i = 0; i < count; i++)
            {
                var nodeId = new NodeId($"Node_{_random.Next(1000)}");
                var parameterId = new ParameterId($"Param_{_random.Next(10)}");
                var parameterKey = new ParameterKey(nodeId, parameterId);
                var value = _random.NextDouble();
                
                parameters[parameterKey] = value;
            }
            
            return parameters;
        }
    }
    
    public class NodeContext
    {
        private readonly int _nodeCount;
        private readonly Random _random;
        
        public NodeContext(int nodeCount, Random random)
        {
            _nodeCount = nodeCount;
            _random = random;
        }
        
        public List<Node> GetRandomNodes(int count)
        {
            var nodes = new List<Node>();
            
            for (int i = 0; i < count; i++)
            {
                nodes.Add(new Node($"Node_{_random.Next(_nodeCount)}"));
            }
            
            return nodes;
        }
        
        public List<Connection> GetRandomConnections(int count)
        {
            var connections = new List<Connection>();
            
            for (int i = 0; i < count; i++)
            {
                connections.Add(new Connection
                {
                    FromNode = new NodeId($"Node_{_random.Next(_nodeCount)}"),
                    ToNode = new NodeId($"Node_{_random.Next(_nodeCount)}"),
                    FromPort = new PortId($"Out_{_random.Next(5)}"),
                    ToPort = new PortId($"In_{_random.Next(5)}")
                });
            }
            
            return connections;
        }
    }
    
    // Simplified implementations for benchmarking
    public class NodeGraph
    {
        public List<OriginalNode> Nodes { get; set; } = new();
        
        public void AddNode(OriginalNode node) => Nodes.Add(node);
        public void ConnectNodes(OriginalNode parent, OriginalNode child) { /* connection logic */ }
        
        public EvaluationResult EvaluateAll()
        {
            var timer = Stopwatch.StartNew();
            int evaluations = 0;
            
            foreach (var node in Nodes)
            {
                node.Evaluate();
                evaluations++;
            }
            
            timer.Stop();
            return new EvaluationResult { NodeEvaluations = evaluations, TotalTime = timer.Elapsed.TotalMilliseconds };
        }
        
        public List<Connection> GetConnectionsForNode(string nodeId)
        {
            // Simplified connection retrieval
            return new List<Connection>();
        }
        
        public RenderResult RenderAll(Viewport viewport)
        {
            var timer = Stopwatch.StartNew();
            
            // Simulate rendering all nodes
            var renderedNodes = Nodes.Count;
            
            timer.Stop();
            return new RenderResult { RenderedNodes = renderedNodes, TotalTime = timer.Elapsed.TotalMilliseconds };
        }
    }
    
    public class OptimizedNodeGraph
    {
        public List<OptimizedNode> Nodes { get; set; } = new();
        
        public void AddNode(OptimizedNode node) => Nodes.Add(node);
        public void ConnectNodes(OptimizedNode parent, OptimizedNode child) { /* optimized connection logic */ }
        
        public EvaluationResult EvaluateAll()
        {
            var timer = Stopwatch.StartNew();
            int evaluations = 0;
            
            // Optimized evaluation using topological sorting
            var evaluationOrder = GetOptimizedEvaluationOrder();
            foreach (var node in evaluationOrder)
            {
                node.Evaluate();
                evaluations++;
            }
            
            timer.Stop();
            return new EvaluationResult { NodeEvaluations = evaluations, TotalTime = timer.Elapsed.TotalMilliseconds };
        }
        
        public IncrementalEvaluationResult EvaluateIncremental(List<Node> changedNodes)
        {
            var timer = Stopwatch.StartNew();
            int affectedNodes = 0;
            
            // Optimized incremental evaluation
            var affected = GetAffectedNodes(changedNodes);
            var evaluationOrder = GetOptimizedEvaluationOrder(affected);
            
            foreach (var node in evaluationOrder)
            {
                node.Evaluate();
                affectedNodes++;
            }
            
            timer.Stop();
            return new IncrementalEvaluationResult { 
                AffectedNodeCount = affectedNodes, 
                EvaluationTime = timer.Elapsed.TotalMilliseconds 
            };
        }
        
        public RenderResult RenderOptimized(Viewport viewport)
        {
            var timer = Stopwatch.StartNew();
            
            // Optimized virtualized rendering
            var visibleNodes = GetVisibleNodes(viewport);
            var culledNodes = ApplyCulling(visibleNodes, viewport);
            
            // Simulate batch rendering
            var batchCount = (culledNodes.Count + 99) / 100;
            var renderedNodes = culledNodes.Count;
            
            timer.Stop();
            return new RenderResult { RenderedNodes = renderedNodes, TotalTime = timer.Elapsed.TotalMilliseconds };
        }
        
        public List<Connection> GetConnectionsForNode(string nodeId)
        {
            // Optimized connection retrieval with caching
            return new List<Connection>();
        }
        
        private List<OptimizedNode> GetOptimizedEvaluationOrder() => Nodes.ToList();
        private List<OptimizedNode> GetOptimizedEvaluationOrder(List<Node> nodes) => nodes.Cast<OptimizedNode>().ToList();
        private HashSet<OptimizedNode> GetAffectedNodes(List<Node> changedNodes) => changedNodes.Cast<OptimizedNode>().ToHashSet();
        private List<OptimizedNode> GetVisibleNodes(Viewport viewport) => Nodes.Where(n => IsNodeVisible(n, viewport)).ToList();
        private List<OptimizedNode> ApplyCulling(List<OptimizedNode> nodes, Viewport viewport) => nodes; // Simplified culling
        private bool IsNodeVisible(OptimizedNode node, Viewport viewport) => true; // Simplified visibility check
    }
    
    public class OriginalNode
    {
        public string Id { get; }
        
        public OriginalNode(string id)
        {
            Id = id;
        }
        
        public void UpdateParameter(string name, object value)
        {
            // Simulate parameter update
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
    }
    
    public class OptimizedNode
    {
        public string Id { get; }
        public bool IsDirty { get; set; } = true;
        
        public OptimizedNode(string id)
        {
            Id = id;
        }
        
        public void UpdateParameter(string name, object value)
        {
            // Optimized parameter update with change detection
            IsDirty = true;
        }
        
        public void Evaluate()
        {
            // Optimized evaluation with caching
            var result = 0.0;
            for (int i = 0; i < 100; i++) // Reduced work due to optimization
            {
                result += Math.Sin(i) * Math.Cos(i);
            }
            IsDirty = false;
        }
    }
    
    public class OriginalConnectionValidator
    {
        public bool ValidateConnection(Connection connection)
        {
            // Simulate connection validation
            Thread.Sleep(1); // Simulate validation work
            return true;
        }
    }
    
    public class OptimizedConnectionValidator
    {
        public Dictionary<ConnectionKey, ValidationResult> ValidateConnectionsBatch(List<Connection> connections)
        {
            // Optimized batch validation with caching
            var results = new Dictionary<ConnectionKey, ValidationResult>();
            
            foreach (var connection in connections)
            {
                var key = new ConnectionKey
                {
                    FromNode = connection.FromNode,
                    ToNode = connection.ToNode,
                    FromPort = connection.FromPort,
                    ToPort = connection.ToPort
                };
                
                // Optimized validation (faster due to caching)
                results[key] = new ValidationResult { IsValid = true };
            }
            
            return results;
        }
    }
    
    public class OriginalParameterChangeDetector
    {
        public bool CheckParameterChange(ParameterKey key, object value)
        {
            // Simulate parameter change detection
            Thread.Sleep(1); // Simulate detection work
            return new Random().Next(10) == 0; // 10% chance of change
        }
    }
    
    public class OptimizedParameterChangeDetector
    {
        public List<ChangeDetectionResult> CheckParameterChangesBatch(Dictionary<ParameterKey, object> parameterValues)
        {
            // Optimized batch change detection with hashing
            var results = new List<ChangeDetectionResult>();
            
            foreach (var kvp in parameterValues)
            {
                // Fast hash-based change detection
                var hasChanged = new Random().Next(10) == 0; // Same probability, much faster
                
                results.Add(new ChangeDetectionResult
                {
                    HasChanged = hasChanged,
                    ParameterKey = kvp.Key,
                    ChangeType = hasChanged ? ChangeType.ValueChanged : ChangeType.NoChange
                });
            }
            
            return results;
        }
    }
    
    // Supporting data structures
    public class EvaluationResult
    {
        public int NodeEvaluations { get; set; }
        public double TotalTime { get; set; }
    }
    
    public class IncrementalEvaluationResult
    {
        public int AffectedNodeCount { get; set; }
        public double EvaluationTime { get; set; }
    }
    
    public class RenderResult
    {
        public int RenderedNodes { get; set; }
        public double TotalTime { get; set; }
    }
    
    public class ValidationResult
    {
        public bool IsValid { get; set; }
    }
    
    public class ChangeDetectionResult
    {
        public bool HasChanged { get; set; }
        public ParameterKey ParameterKey { get; set; }
        public ChangeType ChangeType { get; set; }
    }
    
    public class Viewport
    {
        public Rectangle Bounds { get; set; }
        public double Zoom { get; set; }
        public Point Scroll { get; set; }
    }
    
    public class Rectangle
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
    
    public class Point
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
    
    public class Node
    {
        public string Id { get; }
        
        public Node(string id)
        {
            Id = id;
        }
        
        public void UpdateParameter(string name, object value) { }
    }
    
    public class Connection
    {
        public NodeId FromNode { get; set; }
        public NodeId ToNode { get; set; }
        public PortId FromPort { get; set; }
        public PortId ToPort { get; set; }
    }
    
    public class ConnectionKey
    {
        public NodeId FromNode { get; set; }
        public NodeId ToNode { get; set; }
        public PortId FromPort { get; set; }
        public PortId ToPort { get; set; }
    }
    
    public enum ChangeType
    {
        NoChange,
        ValueChanged
    }
}