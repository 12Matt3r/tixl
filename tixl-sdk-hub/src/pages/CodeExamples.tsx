import { useState } from 'react';
import { Copy, CheckCircle, Code, Database, Network, FileText, Zap, Filter } from 'lucide-react';

export function CodeExamples() {
  const [selectedCategory, setSelectedCategory] = useState('all');
  const [copiedCode, setCopiedCode] = useState<string | null>(null);

  const categories = [
    { id: 'all', label: 'All Examples', count: 12 },
    { id: 'basic', label: 'Basic Operators', count: 4 },
    { id: 'data', label: 'Data Processing', count: 3 },
    { id: 'api', label: 'API Integration', count: 3 },
    { id: 'advanced', label: 'Advanced Patterns', count: 2 }
  ];

  const examples = [
    {
      id: 'hello-world',
      title: 'Hello World Operator',
      description: 'Simple greeting operator that takes a name and returns a personalized message',
      category: 'basic',
      difficulty: 'Beginner',
      icon: Zap,
      code: `import { Operator } from '@tixl/sdk';

class HelloWorldOperator extends Operator {
  async execute(input: { name: string }) {
    const { name } = input;
    
    return {
      message: \`Hello, \${name}! Welcome to TiXL.\`,
      timestamp: new Date().toISOString()
    };
  }
}

export default HelloWorldOperator;`,
      features: ['Input validation', 'Output formatting', 'Basic error handling'],
      usage: 'const result = await operator.execute({ name: "Developer" });'
    },
    {
      id: 'data-transformer',
      title: 'Data Transformer',
      description: 'Transforms and processes data according to custom rules',
      category: 'data',
      difficulty: 'Intermediate',
      icon: Database,
      code: `import { Operator } from '@tixl/sdk';

class DataTransformer extends Operator {
  async execute(input: { data: any[], rules: TransformRule[] }) {
    const { data, rules } = input;
    
    return data.map(item => {
      let transformed = { ...item };
      
      for (const rule of rules) {
        switch (rule.type) {
          case 'uppercase':
            if (transformed[rule.field]) {
              transformed[rule.field] = transformed[rule.field].toUpperCase();
            }
            break;
          case 'normalize':
            if (transformed[rule.field]) {
              transformed[rule.field] = transformed[rule.field].trim().toLowerCase();
            }
            break;
          case 'calculate':
            transformed[rule.field] = this.calculate(rule, transformed);
            break;
        }
      }
      
      return transformed;
    });
  }

  private calculate(rule: TransformRule, item: any): number {
    const { operation, fields } = rule;
    const values = fields.map(field => parseFloat(item[field]) || 0);
    
    switch (operation) {
      case 'sum': return values.reduce((a, b) => a + b, 0);
      case 'average': return values.reduce((a, b) => a + b, 0) / values.length;
      case 'multiply': return values.reduce((a, b) => a * b, 1);
      default: return 0;
    }
  }
}

export default DataTransformer;`,
      features: ['Custom transformation rules', 'Data validation', 'Multiple operations'],
      usage: 'await operator.execute({ data: userData, rules: transformRules });'
    },
    {
      id: 'http-client',
      title: 'HTTP API Client',
      description: 'Makes HTTP requests to external APIs with retry logic and caching',
      category: 'api',
      difficulty: 'Advanced',
      icon: Network,
      code: `import { Operator } from '@tixl/sdk';

class HttpClient extends Operator {
  private cache = new Map<string, any>();
  
  async execute(input: { 
    url: string, 
    method?: string,
    headers?: Record<string, string>,
    body?: any,
    cache?: boolean
  }) {
    const { url, method = 'GET', headers = {}, body, cache = false } = input;
    
    // Check cache first
    if (cache && this.cache.has(url)) {
      return { data: this.cache.get(url), fromCache: true };
    }
    
    try {
      const response = await fetch(url, {
        method,
        headers: {
          'Content-Type': 'application/json',
          ...headers
        },
        body: body ? JSON.stringify(body) : undefined
      });
      
      if (!response.ok) {
        throw new Error(\`HTTP \${response.status}: \${response.statusText}\`);
      }
      
      const data = await response.json();
      
      // Cache successful responses
      if (cache) {
        this.cache.set(url, data);
      }
      
      return {
        data,
        status: response.status,
        headers: Object.fromEntries(response.headers.entries())
      };
      
    } catch (error) {
      return {
        error: error.message,
        url,
        timestamp: new Date().toISOString()
      };
    }
  }
}

export default HttpClient;`,
      features: ['HTTP methods support', 'Error handling', 'Caching mechanism', 'Response processing'],
      usage: 'await operator.execute({ url: "https://api.example.com/data", cache: true });'
    },
    {
      id: 'file-processor',
      title: 'File Processor',
      description: 'Reads, processes, and transforms file contents',
      category: 'data',
      difficulty: 'Intermediate',
      icon: FileText,
      code: `import { Operator } from '@tixl/sdk';
import { readFile, writeFile } from 'fs/promises';

class FileProcessor extends Operator {
  async execute(input: { 
    filename: string,
    operation: 'read' | 'write' | 'transform',
    content?: string,
    encoding?: string
  }) {
    const { filename, operation, content, encoding = 'utf-8' } = input;
    
    switch (operation) {
      case 'read':
        const fileContent = await readFile(filename, encoding);
        return {
          filename,
          content: fileContent,
          size: fileContent.length,
          lines: fileContent.split('\\n').length
        };
        
      case 'write':
        await writeFile(filename, content || '', encoding);
        return {
          filename,
          success: true,
          message: \`File \${filename} written successfully\`
        };
        
      case 'transform':
        const rawContent = await readFile(filename, encoding);
        const transformed = this.transformContent(rawContent);
        
        return {
          filename,
          originalSize: rawContent.length,
          transformedSize: transformed.length,
          content: transformed
        };
    }
  }
  
  private transformContent(content: string): string {
    return content
      .split('\\n')
      .map(line => line.trim())
      .filter(line => line.length > 0)
      .map(line => line.replace(/\\s+/g, ' '))
      .join('\\n');
  }
}

export default FileProcessor;`,
      features: ['Multiple file operations', 'Content transformation', 'File statistics'],
      usage: 'await operator.execute({ filename: "data.txt", operation: "read" });'
    },
    {
      id: 'stream-processor',
      title: 'Stream Processor',
      description: 'Processes large data streams efficiently with backpressure handling',
      category: 'advanced',
      difficulty: 'Advanced',
      icon: Database,
      code: `import { Operator } from '@tixl/sdk';

class StreamProcessor extends Operator {
  async execute(input: { 
    stream: AsyncIterable<any>,
    batchSize?: number,
    transform?: Function,
    filter?: Function
  }) {
    const { stream, batchSize = 100, transform, filter } = input;
    const results = [];
    const batch = [];
    
    for await (const item of stream) {
      // Apply filter if provided
      if (filter && !filter(item)) continue;
      
      // Apply transform if provided
      const processedItem = transform ? transform(item) : item;
      batch.push(processedItem);
      
      // Process in batches
      if (batch.length >= batchSize) {
        results.push(...batch);
        batch.length = 0;
        
        // Allow other operations to run
        await new Promise(resolve => setImmediate(resolve));
      }
    }
    
    // Process remaining items
    if (batch.length > 0) {
      results.push(...batch);
    }
    
    return {
      processedItems: results.length,
      batches: Math.ceil(results.length / batchSize),
      items: results
    };
  }
}

export default StreamProcessor;`,
      features: ['Stream processing', 'Batch operations', 'Backpressure handling', 'Memory efficient'],
      usage: 'await operator.execute({ stream: dataStream, batchSize: 50 });'
    },
    {
      id: 'analytics-aggregator',
      title: 'Analytics Aggregator',
      description: 'Aggregates and analyzes data to generate insights',
      category: 'data',
      difficulty: 'Advanced',
      icon: Database,
      code: `import { Operator } from '@tixl/sdk';

class AnalyticsAggregator extends Operator {
  async execute(input: { 
    data: any[],
    metrics: MetricConfig[],
    groupBy?: string
  }) {
    const { data, metrics, groupBy } = input;
    
    if (groupBy) {
      return this.processGrouped(data, groupBy, metrics);
    }
    
    return this.processUngrouped(data, metrics);
  }
  
  private processGrouped(data: any[], groupBy: string, metrics: MetricConfig[]) {
    const groups = new Map();
    
    data.forEach(item => {
      const key = item[groupBy];
      if (!groups.has(key)) {
        groups.set(key, []);
      }
      groups.get(key).push(item);
    });
    
    const results = [];
    
    for (const [key, items] of groups.entries()) {
      const groupResult: any = { [groupBy]: key };
      
      for (const metric of metrics) {
        groupResult[metric.name] = this.calculateMetric(items, metric);
      }
      
      results.push(groupResult);
    }
    
    return results;
  }
  
  private processUngrouped(data: any[], metrics: MetricConfig[]) {
    const result: any = {};
    
    for (const metric of metrics) {
      result[metric.name] = this.calculateMetric(data, metric);
    }
    
    return result;
  }
  
  private calculateMetric(data: any[], metric: MetricConfig): number {
    const values = data.map(item => parseFloat(item[metric.field]) || 0);
    
    switch (metric.type) {
      case 'sum': return values.reduce((a, b) => a + b, 0);
      case 'average': return values.reduce((a, b) => a + b, 0) / values.length;
      case 'count': return values.length;
      case 'min': return Math.min(...values);
      case 'max': return Math.max(...values);
      case 'stddev': return this.calculateStandardDeviation(values);
      default: return 0;
    }
  }
  
  private calculateStandardDeviation(values: number[]): number {
    const mean = values.reduce((a, b) => a + b, 0) / values.length;
    const squaredDiffs = values.map(value => Math.pow(value - mean, 2));
    const variance = squaredDiffs.reduce((a, b) => a + b, 0) / values.length;
    return Math.sqrt(variance);
  }
}

export default AnalyticsAggregator;`,
      features: ['Multiple aggregation types', 'Grouping support', 'Statistical analysis'],
      usage: 'await operator.execute({ data: salesData, metrics: [{ name: "total", type: "sum", field: "amount" }] });'
    }
  ];

  const filteredExamples = selectedCategory === 'all' 
    ? examples 
    : examples.filter(example => example.category === selectedCategory);

  const copyToClipboard = (code: string, id: string) => {
    navigator.clipboard.writeText(code);
    setCopiedCode(id);
    setTimeout(() => setCopiedCode(null), 2000);
  };

  const getDifficultyColor = (difficulty: string) => {
    switch (difficulty) {
      case 'Beginner': return 'bg-green-100 text-green-800';
      case 'Intermediate': return 'bg-yellow-100 text-yellow-800';
      case 'Advanced': return 'bg-red-100 text-red-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="text-center mb-12">
          <h1 className="text-4xl font-bold text-gray-900 mb-4">Code Examples</h1>
          <p className="text-xl text-gray-600 max-w-3xl mx-auto">
            Explore comprehensive examples of TiXL operators. Learn from real-world implementations 
            and discover best practices for different use cases.
          </p>
        </div>

        {/* Category Filter */}
        <div className="mb-8">
          <div className="flex flex-wrap gap-2 justify-center">
            {categories.map((category) => (
              <button
                key={category.id}
                onClick={() => setSelectedCategory(category.id)}
                className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                  selectedCategory === category.id
                    ? 'bg-blue-600 text-white'
                    : 'bg-white text-gray-700 hover:bg-gray-50 border border-gray-300'
                }`}
              >
                {category.label} ({category.count})
              </button>
            ))}
          </div>
        </div>

        {/* Examples Grid */}
        <div className="grid lg:grid-cols-2 gap-8">
          {filteredExamples.map((example) => {
            const Icon = example.icon;
            return (
              <div key={example.id} className="bg-white rounded-lg shadow-sm border border-gray-200">
                {/* Example Header */}
                <div className="p-6 border-b border-gray-100">
                  <div className="flex items-start justify-between mb-4">
                    <div className="flex items-center space-x-3">
                      <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
                        <Icon className="w-6 h-6 text-blue-600" />
                      </div>
                      <div>
                        <h3 className="text-xl font-semibold text-gray-900">{example.title}</h3>
                        <div className="flex items-center space-x-2 mt-1">
                          <span className={`px-2 py-1 rounded-full text-xs font-medium ${getDifficultyColor(example.difficulty)}`}>
                            {example.difficulty}
                          </span>
                          <span className="text-gray-500 text-sm">{example.category}</span>
                        </div>
                      </div>
                    </div>
                    <button
                      onClick={() => copyToClipboard(example.code, example.id)}
                      className="p-2 text-gray-400 hover:text-gray-600 transition-colors"
                      title="Copy code"
                    >
                      {copiedCode === example.id ? (
                        <CheckCircle className="w-5 h-5 text-green-500" />
                      ) : (
                        <Copy className="w-5 h-5" />
                      )}
                    </button>
                  </div>
                  
                  <p className="text-gray-600 mb-4">{example.description}</p>
                  
                  {/* Features */}
                  <div className="flex flex-wrap gap-2">
                    {example.features.map((feature, index) => (
                      <span
                        key={index}
                        className="px-2 py-1 bg-gray-100 text-gray-700 rounded text-xs"
                      >
                        {feature}
                      </span>
                    ))}
                  </div>
                </div>

                {/* Code Example */}
                <div className="p-6">
                  <div className="flex items-center justify-between mb-3">
                    <h4 className="text-sm font-semibold text-gray-900">Implementation</h4>
                    <div className="flex items-center space-x-1 text-xs text-gray-500">
                      <Code className="w-4 h-4" />
                      TypeScript
                    </div>
                  </div>
                  
                  <div className="bg-gray-900 rounded-lg p-4 overflow-x-auto">
                    <pre className="text-green-400 font-mono text-sm leading-relaxed">
                      <code>{example.code}</code>
                    </pre>
                  </div>
                  
                  {/* Usage */}
                  <div className="mt-4 p-3 bg-blue-50 rounded-lg border border-blue-200">
                    <h5 className="text-sm font-semibold text-blue-900 mb-1">Usage:</h5>
                    <code className="text-blue-800 text-sm font-mono">
                      {example.usage}
                    </code>
                  </div>
                </div>
              </div>
            );
          })}
        </div>

        {/* Additional Resources */}
        <div className="mt-16 bg-white rounded-lg shadow-sm border border-gray-200 p-8">
          <h2 className="text-2xl font-bold text-gray-900 mb-6">Additional Resources</h2>
          <div className="grid md:grid-cols-3 gap-6">
            <div className="text-center p-6">
              <div className="w-12 h-12 bg-purple-100 rounded-lg flex items-center justify-center mx-auto mb-4">
                <Code className="w-6 h-6 text-purple-600" />
              </div>
              <h3 className="text-lg font-semibold text-gray-900 mb-2">Complete Tutorial</h3>
              <p className="text-gray-600 mb-4">
                Step-by-step guide to building your first production-ready operator.
              </p>
              <a href="/quick-start" className="text-blue-600 hover:text-blue-700 font-medium">
                Start Tutorial →
              </a>
            </div>
            <div className="text-center p-6">
              <div className="w-12 h-12 bg-green-100 rounded-lg flex items-center justify-center mx-auto mb-4">
                <Filter className="w-6 h-6 text-green-600" />
              </div>
              <h3 className="text-lg font-semibold text-gray-900 mb-2">Best Practices</h3>
              <p className="text-gray-600 mb-4">
                Learn industry-standard patterns and conventions for operator development.
              </p>
              <a href="/patterns" className="text-blue-600 hover:text-blue-700 font-medium">
                View Patterns →
              </a>
            </div>
            <div className="text-center p-6">
              <div className="w-12 h-12 bg-blue-100 rounded-lg flex items-center justify-center mx-auto mb-4">
                <Database className="w-6 h-6 text-blue-600" />
              </div>
              <h3 className="text-lg font-semibold text-gray-900 mb-2">API Reference</h3>
              <p className="text-gray-600 mb-4">
                Complete documentation of all TiXL SDK APIs and interfaces.
              </p>
              <a href="/documentation" className="text-blue-600 hover:text-blue-700 font-medium">
                Read Docs →
              </a>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}