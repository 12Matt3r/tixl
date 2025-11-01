import { useState } from 'react';
import { Terminal, Play, Bug, Settings, Download, Zap, Shield, Code, TestTube } from 'lucide-react';

export function DevelopmentTools() {
  const [activeTab, setActiveTab] = useState('cli');

  const tools = [
    {
      id: 'cli',
      name: 'TiXL CLI',
      description: 'Command-line interface for developing, testing, and deploying operators',
      icon: Terminal,
      features: [
        'Project scaffolding and initialization',
        'Live development server',
        'Testing and debugging tools',
        'Package building and publishing',
        'Quality checks and validation'
      ],
      installCommand: 'npm install -g @tixl/cli',
      version: '3.2.1',
      status: 'stable'
    },
    {
      id: 'debugger',
      name: 'Visual Debugger',
      description: 'Advanced debugging interface with step-through execution and inspection',
      icon: Bug,
      features: [
        'Step-through execution',
        'Variable inspection',
        'Breakpoint management',
        'Call stack visualization',
        'Memory profiling'
      ],
      installCommand: 'npm install @tixl/debugger',
      version: '2.1.0',
      status: 'beta'
    },
    {
      id: 'tester',
      name: 'Test Runner',
      description: 'Comprehensive testing framework with coverage and performance metrics',
      icon: TestTube,
      features: [
        'Unit and integration testing',
        'Performance benchmarking',
        'Coverage reporting',
        'Mock data generation',
        'CI/CD integration'
      ],
      installCommand: 'npm install @tixl/testing',
      version: '1.8.4',
      status: 'stable'
    },
    {
      id: 'validator',
      name: 'Code Validator',
      description: 'Static analysis tool for code quality and security checking',
      icon: Shield,
      features: [
        'Static code analysis',
        'Security vulnerability scanning',
        'Performance recommendations',
        'Best practices enforcement',
        'Custom rule configuration'
      ],
      installCommand: 'npm install @tixl/validator',
      version: '1.5.2',
      status: 'stable'
    }
  ];

  const quickStart = {
    setup: [
      'npm install -g @tixl/cli',
      'tixl create my-first-operator',
      'cd my-first-operator',
      'npm run dev'
    ],
    test: [
      'tixl test --watch',
      'tixl test --coverage',
      'tixl test --benchmark'
    ],
    build: [
      'tixl build --optimize',
      'tixl validate --strict',
      'tixl publish'
    ]
  };

  const apiExamples = {
    basic: `import { Operator } from '@tixl/sdk';

// Create a basic operator
class MyOperator extends Operator {
  async execute(input) {
    // Your logic here
    return { result: 'success' };
  }
}

export default MyOperator;`,
    advanced: `import { Operator, Context } from '@tixl/sdk';

class AdvancedOperator extends Operator {
  async execute(input: any, context: Context) {
    // Access logger
    context.log.info('Processing input', input);
    
    // Access configuration
    const threshold = context.config.threshold;
    
    // Access state
    const state = await context.state.get('counter') || 0;
    const newCount = state + 1;
    await context.state.set('counter', newCount);
    
    // Access services
    const cache = context.services.get('cache');
    await cache.set('last_processed', newCount);
    
    return {
      count: newCount,
      threshold,
      timestamp: new Date()
    };
  }
}`,
    testing: `import { describe, test, expect } from '@tixl/testing';
import MyOperator from '../src/operator';

describe('MyOperator', () => {
  test('should process input correctly', async () => {
    const operator = new MyOperator();
    const result = await operator.execute({ data: 'test' });
    
    expect(result).toHaveProperty('result');
    expect(result.result).toBe('success');
  });
  
  test('should handle errors gracefully', async () => {
    const operator = new MyOperator();
    
    await expect(
      operator.execute(null)
    ).rejects.toThrow('Invalid input');
  });
});`
  };

  const activeTool = tools.find(tool => tool.id === activeTab);

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="text-center mb-12">
          <h1 className="text-4xl font-bold text-gray-900 mb-4">Development Tools</h1>
          <p className="text-xl text-gray-600 max-w-3xl mx-auto">
            Comprehensive toolkit for developing, testing, debugging, and deploying 
            high-quality TiXL operators efficiently.
          </p>
        </div>

        {/* Tool Overview */}
        <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-6 mb-12">
          {tools.map((tool) => {
            const Icon = tool.icon;
            return (
              <div
                key={tool.id}
                className={`bg-white rounded-lg p-6 border-2 cursor-pointer transition-all ${
                  activeTab === tool.id 
                    ? 'border-blue-500 shadow-md' 
                    : 'border-gray-200 hover:border-gray-300'
                }`}
                onClick={() => setActiveTab(tool.id)}
              >
                <div className="w-12 h-12 bg-blue-100 rounded-lg flex items-center justify-center mb-4">
                  <Icon className="w-6 h-6 text-blue-600" />
                </div>
                <h3 className="text-lg font-semibold text-gray-900 mb-2">{tool.name}</h3>
                <p className="text-gray-600 text-sm mb-3">{tool.description}</p>
                <div className="flex items-center justify-between">
                  <span className="text-sm text-gray-500">v{tool.version}</span>
                  <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                    tool.status === 'stable' ? 'bg-green-100 text-green-800' : 'bg-yellow-100 text-yellow-800'
                  }`}>
                    {tool.status}
                  </span>
                </div>
              </div>
            );
          })}
        </div>

        {/* Active Tool Details */}
        {activeTool && (
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 mb-12">
            <div className="p-8">
              <div className="flex items-center space-x-3 mb-6">
                <activeTool.icon className="w-8 h-8 text-blue-600" />
                <div>
                  <h2 className="text-2xl font-bold text-gray-900">{activeTool.name}</h2>
                  <p className="text-gray-600">{activeTool.description}</p>
                </div>
              </div>

              <div className="grid lg:grid-cols-2 gap-8">
                {/* Features */}
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">Features</h3>
                  <ul className="space-y-2">
                    {activeTool.features.map((feature, index) => (
                      <li key={index} className="flex items-center space-x-2">
                        <div className="w-2 h-2 bg-blue-500 rounded-full"></div>
                        <span className="text-gray-600">{feature}</span>
                      </li>
                    ))}
                  </ul>
                </div>

                {/* Installation */}
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">Installation</h3>
                  <div className="bg-gray-900 rounded-lg p-4 mb-4">
                    <div className="text-green-400 font-mono text-sm">
                      {activeTool.installCommand}
                    </div>
                  </div>
                  <div className="space-y-2 text-sm text-gray-600">
                    <div>Version: {activeTool.version}</div>
                    <div>Status: {activeTool.status}</div>
                    <div>License: MIT</div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Quick Start Commands */}
        <div className="grid lg:grid-cols-3 gap-6 mb-12">
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center">
              <Zap className="w-5 h-5 text-blue-600 mr-2" />
              Quick Setup
            </h3>
            <div className="space-y-3">
              {quickStart.setup.map((command, index) => (
                <div key={index} className="bg-gray-900 rounded p-3">
                  <div className="text-green-400 font-mono text-sm">{command}</div>
                </div>
              ))}
            </div>
          </div>

          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center">
              <TestTube className="w-5 h-5 text-green-600 mr-2" />
              Testing
            </h3>
            <div className="space-y-3">
              {quickStart.test.map((command, index) => (
                <div key={index} className="bg-gray-900 rounded p-3">
                  <div className="text-green-400 font-mono text-sm">{command}</div>
                </div>
              ))}
            </div>
          </div>

          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center">
              <Download className="w-5 h-5 text-purple-600 mr-2" />
              Build & Deploy
            </h3>
            <div className="space-y-3">
              {quickStart.build.map((command, index) => (
                <div key={index} className="bg-gray-900 rounded p-3">
                  <div className="text-green-400 font-mono text-sm">{command}</div>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Code Examples */}
        <div className="mb-12">
          <h2 className="text-2xl font-bold text-gray-900 mb-8">API Examples</h2>
          <div className="grid lg:grid-cols-1 gap-6">
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <h3 className="text-lg font-semibold text-gray-900 mb-4">Basic Operator</h3>
              <div className="bg-gray-900 rounded-lg p-4 overflow-x-auto">
                <pre className="text-green-400 font-mono text-sm">
                  <code>{apiExamples.basic}</code>
                </pre>
              </div>
            </div>

            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <h3 className="text-lg font-semibold text-gray-900 mb-4">Advanced Features</h3>
              <div className="bg-gray-900 rounded-lg p-4 overflow-x-auto">
                <pre className="text-green-400 font-mono text-sm">
                  <code>{apiExamples.advanced}</code>
                </pre>
              </div>
            </div>

            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <h3 className="text-lg font-semibold text-gray-900 mb-4">Testing Example</h3>
              <div className="bg-gray-900 rounded-lg p-4 overflow-x-auto">
                <pre className="text-green-400 font-mono text-sm">
                  <code>{apiExamples.testing}</code>
                </pre>
              </div>
            </div>
          </div>
        </div>

        {/* Configuration */}
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-8">
          <h2 className="text-2xl font-bold text-gray-900 mb-6">Configuration</h2>
          <div className="grid lg:grid-cols-2 gap-8">
            <div>
              <h3 className="text-lg font-semibold text-gray-900 mb-4">tixl.config.js</h3>
              <div className="bg-gray-900 rounded-lg p-4">
                <pre className="text-green-400 font-mono text-sm">
{`module.exports = {
  compiler: {
    target: 'es2020',
    module: 'commonjs',
    sourceMap: true,
    optimization: true
  },
  testing: {
    framework: 'jest',
    coverage: {
      threshold: 90,
      exclude: ['src/types/**']
    }
  },
  validation: {
    rules: {
      security: 'strict',
      performance: 'recommended'
    }
  },
  plugins: [
    '@tixl/plugin-validator',
    '@tixl/plugin-profiler'
  ]
};`}
                </pre>
              </div>
            </div>

            <div>
              <h3 className="text-lg font-semibold text-gray-900 mb-4">Environment Variables</h3>
              <div className="space-y-3">
                <div>
                  <code className="bg-gray-100 px-2 py-1 rounded text-sm font-mono">TIXL_LOG_LEVEL</code>
                  <p className="text-gray-600 text-sm mt-1">Set logging level (debug, info, warn, error)</p>
                </div>
                <div>
                  <code className="bg-gray-100 px-2 py-1 rounded text-sm font-mono">TIXL_API_KEY</code>
                  <p className="text-gray-600 text-sm mt-1">API key for marketplace operations</p>
                </div>
                <div>
                  <code className="bg-gray-100 px-2 py-1 rounded text-sm font-mono">TIXL_CACHE_DIR</code>
                  <p className="text-gray-600 text-sm mt-1">Directory for build cache (optional)</p>
                </div>
                <div>
                  <code className="bg-gray-100 px-2 py-1 rounded text-sm font-mono">TIXL_DISABLE_TELEMETRY</code>
                  <p className="text-gray-600 text-sm mt-1">Disable usage analytics (optional)</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}