import { useState } from 'react';
import { ChevronDown, ChevronRight, Code, BookOpen, Zap, Shield } from 'lucide-react';

export function Documentation() {
  const [expandedSections, setExpandedSections] = useState<string[]>(['core']);

  const toggleSection = (sectionId: string) => {
    setExpandedSections(prev => 
      prev.includes(sectionId) 
        ? prev.filter(id => id !== sectionId)
        : [...prev, sectionId]
    );
  };

  const documentationSections = [
    {
      id: 'core',
      title: 'Core SDK',
      icon: Zap,
      description: 'Fundamental concepts and APIs',
      subsections: [
        { title: 'Getting Started', id: 'getting-started', content: 'Introduction to the TiXL SDK and basic setup' },
        { title: 'Operator Basics', id: 'operator-basics', content: 'Understanding operators and their lifecycle' },
        { title: 'API Reference', id: 'api-reference', content: 'Complete API documentation' },
        { title: 'Data Types', id: 'data-types', content: 'Supported data types and structures' }
      ]
    },
    {
      id: 'advanced',
      title: 'Advanced Topics',
      icon: Code,
      description: 'Complex features and patterns',
      subsections: [
        { title: 'Custom Operators', id: 'custom-operators', content: 'Building sophisticated custom operators' },
        { title: 'Performance Optimization', id: 'performance', content: 'Optimizing operator performance' },
        { title: 'Error Handling', id: 'error-handling', content: 'Robust error handling patterns' },
        { title: 'Testing', id: 'testing', content: 'Comprehensive testing strategies' }
      ]
    },
    {
      id: 'security',
      title: 'Security & Compliance',
      icon: Shield,
      description: 'Security best practices and compliance',
      subsections: [
        { title: 'Security Guidelines', id: 'security', content: 'Security best practices for operators' },
        { title: 'Data Protection', id: 'data-protection', content: 'Protecting sensitive data in operators' },
        { title: 'Authentication', id: 'authentication', content: 'Implementing secure authentication' },
        { title: 'Compliance', id: 'compliance', content: 'Meeting compliance requirements' }
      ]
    },
    {
      id: 'integration',
      title: 'Integration',
      icon: BookOpen,
      description: 'Integration with TiXL ecosystem',
      subsections: [
        { title: 'Pipeline Integration', id: 'pipeline', content: 'Integrating operators into pipelines' },
        { title: 'Event Handling', id: 'events', content: 'Handling events and callbacks' },
        { title: 'State Management', id: 'state', content: 'Managing operator state effectively' },
        { title: 'Configuration', id: 'configuration', content: 'Operator configuration patterns' }
      ]
    }
  ];

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mb-12">
          <h1 className="text-4xl font-bold text-gray-900 mb-4">SDK Documentation</h1>
          <p className="text-xl text-gray-600 max-w-3xl">
            Comprehensive documentation for developing TiXL operators. Find everything you need 
            from basic concepts to advanced implementation patterns.
          </p>
        </div>

        {/* Quick Navigation */}
        <div className="mb-12">
          <h2 className="text-2xl font-semibold text-gray-900 mb-6">Quick Navigation</h2>
          <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="p-4 bg-blue-50 rounded-lg border border-blue-200">
              <h3 className="font-semibold text-blue-900 mb-2">Quick Start</h3>
              <p className="text-blue-700 text-sm">Get up and running in minutes</p>
            </div>
            <div className="p-4 bg-green-50 rounded-lg border border-green-200">
              <h3 className="font-semibold text-green-900 mb-2">API Reference</h3>
              <p className="text-green-700 text-sm">Complete API documentation</p>
            </div>
            <div className="p-4 bg-purple-50 rounded-lg border border-purple-200">
              <h3 className="font-semibold text-purple-900 mb-2">Examples</h3>
              <p className="text-purple-700 text-sm">Real-world code examples</p>
            </div>
            <div className="p-4 bg-orange-50 rounded-lg border border-orange-200">
              <h3 className="font-semibold text-orange-900 mb-2">Best Practices</h3>
              <p className="text-orange-700 text-sm">Industry best practices</p>
            </div>
          </div>
        </div>

        <div className="grid lg:grid-cols-4 gap-8">
          {/* Sidebar Navigation */}
          <div className="lg:col-span-1">
            <div className="sticky top-24">
              <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <h3 className="text-lg font-semibold text-gray-900 mb-4">Documentation Sections</h3>
                <nav className="space-y-2">
                  {documentationSections.map((section) => {
                    const Icon = section.icon;
                    const isExpanded = expandedSections.includes(section.id);
                    return (
                      <div key={section.id}>
                        <button
                          onClick={() => toggleSection(section.id)}
                          className="w-full flex items-center justify-between p-2 text-left hover:bg-gray-50 rounded-md"
                        >
                          <div className="flex items-center space-x-2">
                            <Icon className="w-4 h-4 text-gray-500" />
                            <span className="font-medium text-gray-900">{section.title}</span>
                          </div>
                          {isExpanded ? (
                            <ChevronDown className="w-4 h-4 text-gray-500" />
                          ) : (
                            <ChevronRight className="w-4 h-4 text-gray-500" />
                          )}
                        </button>
                        {isExpanded && (
                          <div className="ml-6 mt-2 space-y-1">
                            {section.subsections.map((subsection) => (
                              <a
                                key={subsection.id}
                                href={`#${subsection.id}`}
                                className="block p-2 text-sm text-gray-600 hover:text-blue-600 hover:bg-blue-50 rounded-md"
                              >
                                {subsection.title}
                              </a>
                            ))}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </nav>
              </div>
            </div>
          </div>

          {/* Main Content */}
          <div className="lg:col-span-3">
            <div className="space-y-12">
              {/* Getting Started */}
              <section id="getting-started" className="bg-white rounded-lg shadow-sm border border-gray-200 p-8">
                <h2 className="text-3xl font-bold text-gray-900 mb-6">Getting Started</h2>
                <div className="prose max-w-none">
                  <p className="text-lg text-gray-600 mb-6">
                    Welcome to the TiXL Operator SDK! This guide will help you create your first operator 
                    and understand the core concepts of the TiXL ecosystem.
                  </p>
                  
                  <h3 className="text-xl font-semibold text-gray-900 mb-4">Prerequisites</h3>
                  <ul className="list-disc pl-6 text-gray-600 space-y-2">
                    <li>Node.js 16+ and npm/pnpm</li>
                    <li>Basic understanding of JavaScript/TypeScript</li>
                    <li>TiXL SDK installed globally</li>
                  </ul>

                  <h3 className="text-xl font-semibold text-gray-900 mb-4 mt-8">Installation</h3>
                  <div className="bg-gray-900 rounded-lg p-4 text-green-400 font-mono text-sm">
                    npm install -g @tixl/sdk<br/>
                    tixl --version
                  </div>

                  <h3 className="text-xl font-semibold text-gray-900 mb-4 mt-8">Your First Operator</h3>
                  <div className="bg-gray-900 rounded-lg p-4 text-green-400 font-mono text-sm mb-4">
                    <div className="mb-2">tixl create my-first-operator</div>
                    <div className="mb-2">cd my-first-operator</div>
                    <div>npm run dev</div>
                  </div>
                  
                  <p className="text-gray-600">
                    This creates a new operator project with a development server running. 
                    You can now start building your operator!
                  </p>
                </div>
              </section>

              {/* Operator Basics */}
              <section id="operator-basics" className="bg-white rounded-lg shadow-sm border border-gray-200 p-8">
                <h2 className="text-3xl font-bold text-gray-900 mb-6">Operator Basics</h2>
                <div className="prose max-w-none">
                  <p className="text-lg text-gray-600 mb-6">
                    Operators are the building blocks of TiXL workflows. They process data, 
                    perform transformations, and enable complex automation scenarios.
                  </p>

                  <h3 className="text-xl font-semibold text-gray-900 mb-4">What is an Operator?</h3>
                  <p className="text-gray-600 mb-4">
                    An operator is a self-contained unit of functionality that can:
                  </p>
                  <ul className="list-disc pl-6 text-gray-600 space-y-2 mb-6">
                    <li>Receive input data and configuration</li>
                    <li>Process or transform data</li>
                    <li>Produce output data</li>
                    <li>Handle errors gracefully</li>
                    <li>Communicate with external systems</li>
                  </ul>

                  <h3 className="text-xl font-semibold text-gray-900 mb-4">Operator Lifecycle</h3>
                  <div className="bg-gray-50 rounded-lg p-6 border border-gray-200">
                    <div className="space-y-4">
                      <div className="flex items-center">
                        <div className="w-3 h-3 bg-blue-500 rounded-full mr-4"></div>
                        <div>
                          <h4 className="font-semibold text-gray-900">1. Initialization</h4>
                          <p className="text-gray-600 text-sm">Operator is created and configured</p>
                        </div>
                      </div>
                      <div className="flex items-center">
                        <div className="w-3 h-3 bg-green-500 rounded-full mr-4"></div>
                        <div>
                          <h4 className="font-semibold text-gray-900">2. Processing</h4>
                          <p className="text-gray-600 text-sm">Input data is processed and transformed</p>
                        </div>
                      </div>
                      <div className="flex items-center">
                        <div className="w-3 h-3 bg-purple-500 rounded-full mr-4"></div>
                        <div>
                          <h4 className="font-semibold text-gray-900">3. Output</h4>
                          <p className="text-gray-600 text-sm">Results are returned to the workflow</p>
                        </div>
                      </div>
                      <div className="flex items-center">
                        <div className="w-3 h-3 bg-gray-500 rounded-full mr-4"></div>
                        <div>
                          <h4 className="font-semibold text-gray-900">4. Cleanup</h4>
                          <p className="text-gray-600 text-sm">Resources are freed and cleanup performed</p>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </section>

              {/* API Reference Preview */}
              <section id="api-reference" className="bg-white rounded-lg shadow-sm border border-gray-200 p-8">
                <h2 className="text-3xl font-bold text-gray-900 mb-6">API Reference</h2>
                <div className="prose max-w-none">
                  <p className="text-lg text-gray-600 mb-6">
                    The TiXL SDK provides a comprehensive API for building operators. 
                    Here are the key interfaces you'll work with:
                  </p>

                  <h3 className="text-xl font-semibold text-gray-900 mb-4">Base Operator Interface</h3>
                  <div className="bg-gray-900 rounded-lg p-4 text-green-400 font-mono text-sm mb-6">
                    <div className="text-blue-400">interface</div> <div className="text-yellow-400">Operator</div> {'{'}
                    <div className="ml-4">execute(input: <span className="text-blue-400">any</span>): <span className="text-blue-400">Promise</span>&lt;<span className="text-blue-400">any</span>&gt;;</div>
                    <div className="ml-4">validate(config: <span className="text-blue-400">any</span>): <span className="text-blue-400">boolean</span>;</div>
                    <div className="ml-4">cleanup(): <span className="text-blue-400">Promise</span>&lt;<span className="text-blue-400">void</span>&gt;;</div>
                    {'}'}
                  </div>

                  <h3 className="text-xl font-semibold text-gray-900 mb-4">Context Object</h3>
                  <div className="bg-gray-900 rounded-lg p-4 text-green-400 font-mono text-sm">
                    <div className="text-blue-400">interface</div> <div className="text-yellow-400">OperatorContext</div> {'{'}
                    <div className="ml-4">log: <span className="text-blue-400">Logger</span>;</div>
                    <div className="ml-4">config: <span className="text-blue-400">any</span>;</div>
                    <div className="ml-4">state: <span className="text-blue-400">OperatorState</span>;</div>
                    <div className="ml-4">services: <span className="text-blue-400">Record</span>&lt;<span className="text-blue-400">string</span>, <span className="text-blue-400">any</span>&gt;;</div>
                    {'}'}
                  </div>
                </div>
              </section>

              {/* Data Types */}
              <section id="data-types" className="bg-white rounded-lg shadow-sm border border-gray-200 p-8">
                <h2 className="text-3xl font-bold text-gray-900 mb-6">Data Types</h2>
                <div className="prose max-w-none">
                  <p className="text-lg text-gray-600 mb-6">
                    TiXL operators work with structured data using a powerful type system 
                    that ensures compatibility and type safety.
                  </p>

                  <div className="grid md:grid-cols-2 gap-6">
                    <div>
                      <h3 className="text-xl font-semibold text-gray-900 mb-4">Supported Types</h3>
                      <ul className="space-y-2 text-gray-600">
                        <li><code className="bg-gray-100 px-2 py-1 rounded">string</code> - Text data</li>
                        <li><code className="bg-gray-100 px-2 py-1 rounded">number</code> - Numeric values</li>
                        <li><code className="bg-gray-100 px-2 py-1 rounded">boolean</code> - True/false values</li>
                        <li><code className="bg-gray-100 px-2 py-1 rounded">object</code> - Complex objects</li>
                        <li><code className="bg-gray-100 px-2 py-1 rounded">array</code> - Collections</li>
                        <li><code className="bg-gray-100 px-2 py-1 rounded">date</code> - Date/time values</li>
                      </ul>
                    </div>
                    <div>
                      <h3 className="text-xl font-semibold text-gray-900 mb-4">Special Types</h3>
                      <ul className="space-y-2 text-gray-600">
                        <li><code className="bg-gray-100 px-2 py-1 rounded">stream</code> - Data streams</li>
                        <li><code className="bg-gray-100 px-2 py-1 rounded">buffer</code> - Binary data</li>
                        <li><code className="bg-gray-100 px-2 py-1 rounded">json</code> - JSON structures</li>
                        <li><code className="bg-gray-100 px-2 py-1 rounded">csv</code> - CSV formatted data</li>
                        <li><code className="bg-gray-100 px-2 py-1 rounded">xml</code> - XML documents</li>
                      </ul>
                    </div>
                  </div>

                  <h3 className="text-xl font-semibold text-gray-900 mb-4 mt-8">Type Validation</h3>
                  <div className="bg-gray-900 rounded-lg p-4 text-green-400 font-mono text-sm">
                    <div className="text-purple-400">import</div> {'{'} <span className="text-yellow-400">validateType</span>, <span className="text-yellow-400">Type</span> {'}'} <span className="text-purple-400">from</span> <span className="text-green-400">'@tixl/sdk'</span>;<br/><br/>
                    <div className="text-blue-400">const</div> <div className="text-yellow-400">schema</div> = {'{'}
                    <div className="ml-4">name: <span className="text-yellow-400">Type.STRING</span>,</div>
                    <div className="ml-4">age: <span className="text-yellow-400">Type.NUMBER</span>,</div>
                    <div className="ml-4">isActive: <span className="text-yellow-400">Type.BOOLEAN</span></div>
                    {'}'};<br/><br/>
                    <div className="text-blue-400">const</div> <div className="text-yellow-400">isValid</div> = <span className="text-yellow-400">validateType</span>(data, schema);
                  </div>
                </div>
              </section>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}