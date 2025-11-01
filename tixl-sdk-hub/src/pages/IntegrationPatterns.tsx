import { useState } from 'react';
import { Layers, ArrowRight, Code, Network, Database, Zap, Shield, RefreshCw } from 'lucide-react';

export function IntegrationPatterns() {
  const [selectedPattern, setSelectedPattern] = useState('pipeline');

  const patterns = [
    {
      id: 'pipeline',
      name: 'Pipeline Pattern',
      description: 'Chain operators together to create data processing workflows',
      icon: Layers,
      useCase: 'ETL processes, data transformations, multi-step workflows',
      complexity: 'Beginner',
      performance: 'High',
      examples: [
        {
          title: 'Data Processing Pipeline',
          description: 'Transform raw data through multiple processing steps'
        }
      ]
    },
    {
      id: 'event-driven',
      name: 'Event-Driven Pattern',
      description: 'Operators respond to events and trigger actions',
      icon: Network,
      useCase: 'Real-time processing, webhooks, system notifications',
      complexity: 'Intermediate',
      performance: 'Very High',
      examples: [
        {
          title: 'Event Stream Processor',
          description: 'Process events from multiple sources in real-time'
        }
      ]
    },
    {
      id: 'microservice',
      name: 'Microservice Pattern',
      description: 'Operators act as independent microservices with well-defined APIs',
      icon: Zap,
      useCase: 'Service-oriented architectures, distributed systems',
      complexity: 'Advanced',
      performance: 'High',
      examples: [
        {
          title: 'API Gateway Service',
          description: 'Handle external API requests with routing and caching'
        }
      ]
    },
    {
      id: 'data-lake',
      name: 'Data Lake Pattern',
      description: 'Operators manage and query large datasets efficiently',
      icon: Database,
      useCase: 'Big data processing, analytics, machine learning',
      complexity: 'Advanced',
      performance: 'Very High',
      examples: [
        {
          title: 'Data Lake Query Engine',
          description: 'Query and aggregate large datasets from various sources'
        }
      ]
    }
  ];

  const codeExamples = {
    pipeline: `import { Pipeline } from '@tixl/sdk';

// Create a data processing pipeline
const pipeline = new Pipeline([
  new DataValidator(),
  new DataCleaner(),
  new DataTransformer(),
  new DataEnricher(),
  new OutputFormatter()
]);

// Execute the pipeline
const result = await pipeline.execute({
  source: 'customers.csv',
  rules: validationRules
});

console.log('Processed records:', result.count);`,
    event: `import { EventDrivenOperator } from '@tixl/sdk';

class EventProcessor extends EventDrivenOperator {
  async handleEvent(event: Event) {
    switch (event.type) {
      case 'USER_REGISTERED':
        await this.sendWelcomeEmail(event.data);
        await this.updateAnalytics(event.data);
        break;
        
      case 'ORDER_CREATED':
        await this.processPayment(event.data);
        await this.updateInventory(event.data);
        break;
        
      case 'PAYMENT_COMPLETED':
        await this.shipOrder(event.data);
        await this.notifyCustomer(event.data);
        break;
    }
  }
}`,
    microservice: `import { MicroserviceOperator } from '@tixl/sdk';

class UserService extends MicroserviceOperator {
  async execute(input: UserRequest) {
    const { action, data } = input;
    
    switch (action) {
      case 'get':
        return this.getUser(data.id);
        
      case 'create':
        return this.createUser(data);
        
      case 'update':
        return this.updateUser(data.id, data.updates);
        
      case 'delete':
        return this.deleteUser(data.id);
    }
  }
  
  // Health check endpoint
  async healthCheck() {
    return { status: 'healthy', timestamp: new Date() };
  }
}`,
    datalake: `import { DataLakeOperator } from '@tixl/sdk';

class DataLakeQuery extends DataLakeOperator {
  async execute(input: QueryRequest) {
    const { dataset, query, filters, aggregations } = input;
    
    // Build optimized query
    const queryBuilder = this.createQueryBuilder(dataset)
      .select(query.fields)
      .where(filters)
      .groupBy(aggregations.groupBy)
      .limit(query.limit);
    
    // Execute with caching
    const result = await this.executeQuery(queryBuilder, {
      useCache: true,
      cacheTTL: '1h'
    });
    
    return {
      data: result.rows,
      metadata: {
        totalRows: result.count,
        executionTime: result.executionTime,
        cached: result.fromCache
      }
    };
  }
}`
  };

  const bestPractices = [
    {
      category: 'Performance',
      icon: Zap,
      practices: [
        {
          title: 'Lazy Loading',
          description: 'Load dependencies only when needed to reduce startup time',
          implementation: 'Use dynamic imports and conditional loading'
        },
        {
          title: 'Connection Pooling',
          description: 'Reuse database connections to improve performance',
          implementation: 'Maintain a pool of reusable connections'
        },
        {
          title: 'Batch Processing',
          description: 'Process multiple items together to reduce overhead',
          implementation: 'Buffer items and process in batches'
        }
      ]
    },
    {
      category: 'Reliability',
      icon: Shield,
      practices: [
        {
          title: 'Circuit Breaker',
          description: 'Prevent cascade failures by stopping requests to failing services',
          implementation: 'Track failure rates and implement fallback logic'
        },
        {
          title: 'Retry Logic',
          description: 'Automatically retry failed operations with exponential backoff',
          implementation: 'Implement configurable retry policies'
        },
        {
          title: 'Timeout Management',
          description: 'Set appropriate timeouts to prevent hanging operations',
          implementation: 'Use promise timeout wrappers'
        }
      ]
    },
    {
      category: 'Scalability',
      icon: Network,
      practices: [
        {
          title: 'Horizontal Scaling',
          description: 'Design operators to work across multiple instances',
          implementation: 'Use stateless design and shared storage'
        },
        {
          title: 'Load Balancing',
          description: 'Distribute workload across multiple operator instances',
          implementation: 'Implement load balancing at the orchestrator level'
        },
        {
          title: 'Resource Management',
          description: 'Monitor and limit resource usage to prevent overload',
          implementation: 'Track memory and CPU usage, implement limits'
        }
      ]
    },
    {
      category: 'Observability',
      icon: Code,
      practices: [
        {
          title: 'Structured Logging',
          description: 'Use consistent logging patterns for better debugging',
          implementation: 'Implement structured logging with context'
        },
        {
          title: 'Metrics Collection',
          description: 'Collect performance and business metrics for monitoring',
          implementation: 'Use metrics libraries and dashboards'
        },
        {
          title: 'Distributed Tracing',
          description: 'Trace requests across multiple operators for debugging',
          implementation: 'Implement correlation IDs and trace context'
        }
      ]
    }
  ];

  const activePattern = patterns.find(p => p.id === selectedPattern);

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="text-center mb-12">
          <h1 className="text-4xl font-bold text-gray-900 mb-4">Integration Patterns</h1>
          <p className="text-xl text-gray-600 max-w-3xl mx-auto">
            Learn proven patterns and best practices for integrating operators 
            into robust, scalable, and maintainable systems.
          </p>
        </div>

        {/* Pattern Selection */}
        <div className="mb-12">
          <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-4">
            {patterns.map((pattern) => {
              const Icon = pattern.icon;
              return (
                <button
                  key={pattern.id}
                  onClick={() => setSelectedPattern(pattern.id)}
                  className={`p-6 text-left rounded-lg border-2 transition-all ${
                    selectedPattern === pattern.id
                      ? 'border-blue-500 bg-blue-50'
                      : 'border-gray-200 bg-white hover:border-gray-300'
                  }`}
                >
                  <Icon className={`w-8 h-8 mb-3 ${
                    selectedPattern === pattern.id ? 'text-blue-600' : 'text-gray-500'
                  }`} />
                  <h3 className="text-lg font-semibold text-gray-900 mb-2">{pattern.name}</h3>
                  <p className="text-gray-600 text-sm mb-3">{pattern.description}</p>
                  <div className="flex justify-between text-xs">
                    <span className={`px-2 py-1 rounded ${
                      pattern.complexity === 'Beginner' ? 'bg-green-100 text-green-800' :
                      pattern.complexity === 'Intermediate' ? 'bg-yellow-100 text-yellow-800' :
                      'bg-red-100 text-red-800'
                    }`}>
                      {pattern.complexity}
                    </span>
                    <span className="text-gray-500">{pattern.performance} Performance</span>
                  </div>
                </button>
              );
            })}
          </div>
        </div>

        {/* Active Pattern Details */}
        {activePattern && (
          <div className="mb-12">
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-8">
              <div className="flex items-center space-x-3 mb-6">
                <activePattern.icon className="w-8 h-8 text-blue-600" />
                <div>
                  <h2 className="text-2xl font-bold text-gray-900">{activePattern.name}</h2>
                  <p className="text-gray-600">{activePattern.description}</p>
                </div>
              </div>

              <div className="grid lg:grid-cols-2 gap-8 mb-8">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">Use Cases</h3>
                  <p className="text-gray-600">{activePattern.useCase}</p>
                </div>
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">Characteristics</h3>
                  <div className="space-y-2">
                    <div className="flex justify-between">
                      <span className="text-gray-600">Complexity:</span>
                      <span className="font-medium">{activePattern.complexity}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-gray-600">Performance:</span>
                      <span className="font-medium">{activePattern.performance}</span>
                    </div>
                  </div>
                </div>
              </div>

              <div className="mb-8">
                <h3 className="text-lg font-semibold text-gray-900 mb-4">Implementation Example</h3>
                <div className="bg-gray-900 rounded-lg p-6 overflow-x-auto">
                  <pre className="text-green-400 font-mono text-sm">
                    <code>{codeExamples[selectedPattern]}</code>
                  </pre>
                </div>
              </div>

              {activePattern.examples.map((example, index) => (
                <div key={index} className="bg-blue-50 border border-blue-200 rounded-lg p-6">
                  <h4 className="text-lg font-semibold text-blue-900 mb-2">{example.title}</h4>
                  <p className="text-blue-700">{example.description}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Best Practices */}
        <div className="mb-12">
          <h2 className="text-3xl font-bold text-gray-900 mb-8">Best Practices</h2>
          <div className="space-y-8">
            {bestPractices.map((category, index) => {
              const Icon = category.icon;
              return (
                <div key={index} className="bg-white rounded-lg shadow-sm border border-gray-200 p-8">
                  <div className="flex items-center space-x-3 mb-6">
                    <Icon className="w-6 h-6 text-blue-600" />
                    <h3 className="text-xl font-semibold text-gray-900">{category.category}</h3>
                  </div>
                  <div className="grid lg:grid-cols-3 gap-6">
                    {category.practices.map((practice, practiceIndex) => (
                      <div key={practiceIndex} className="border border-gray-200 rounded-lg p-6">
                        <h4 className="text-lg font-semibold text-gray-900 mb-3">{practice.title}</h4>
                        <p className="text-gray-600 mb-4">{practice.description}</p>
                        <div className="bg-gray-50 border border-gray-200 rounded p-3">
                          <h5 className="text-sm font-semibold text-gray-900 mb-1">Implementation:</h5>
                          <p className="text-sm text-gray-600">{practice.implementation}</p>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Architecture Decision Guide */}
        <div className="bg-gradient-to-r from-blue-600 to-purple-600 rounded-lg p-8 text-white">
          <h2 className="text-2xl font-bold mb-6">Choosing the Right Pattern</h2>
          <div className="grid md:grid-cols-2 gap-8">
            <div>
              <h3 className="text-lg font-semibold mb-4">Consider Pipeline Pattern When:</h3>
              <ul className="space-y-2 text-blue-100">
                <li>• You have sequential data processing steps</li>
                <li>• Each step transforms or validates data</li>
                <li>• You need audit trail of processing steps</li>
                <li>• Performance is important but not critical</li>
              </ul>
            </div>
            <div>
              <h3 className="text-lg font-semibold mb-4">Consider Event-Driven When:</h3>
              <ul className="space-y-2 text-blue-100">
                <li>• You need real-time processing</li>
                <li>• Events can be processed independently</li>
                <li>• You have high message volumes</li>
                <li>• You need loose coupling between components</li>
              </ul>
            </div>
            <div>
              <h3 className="text-lg font-semibold mb-4">Consider Microservice When:</h3>
              <ul className="space-y-2 text-blue-100">
                <li>• You need independent deployment</li>
                <li>• Teams work on different services</li>
                <li>• You have complex business logic</li>
                <li>• You need service-level scaling</li>
              </ul>
            </div>
            <div>
              <h3 className="text-lg font-semibold mb-4">Consider Data Lake When:</h3>
              <ul className="space-y-2 text-blue-100">
                <li>• You work with large datasets</li>
                <li>• You need complex analytical queries</li>
                <li>• You have diverse data sources</li>
                <li>• Performance at scale is critical</li>
              </ul>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}