import { useState } from 'react';
import { Play, CheckCircle, ArrowRight, Code, Download, Settings, Rocket } from 'lucide-react';

export function QuickStart() {
  const [completedSteps, setCompletedSteps] = useState<number[]>([]);
  const [activeStep, setActiveStep] = useState(0);

  const toggleStep = (stepIndex: number) => {
    setCompletedSteps(prev => 
      prev.includes(stepIndex) 
        ? prev.filter(i => i !== stepIndex)
        : [...prev, stepIndex]
    );
  };

  const steps = [
    {
      id: 0,
      title: 'Install TiXL SDK',
      description: 'Set up your development environment',
      icon: Download,
      duration: '2 minutes',
      content: {
        overview: 'Install the TiXL SDK globally on your system',
        prerequisites: ['Node.js 16+ installed', 'npm or pnpm package manager', 'Code editor (VS Code recommended)'],
        commands: [
          'npm install -g @tixl/sdk',
          'tixl --version',
          'tixl --help'
        ],
        verification: 'Verify installation by checking version and help output'
      }
    },
    {
      id: 1,
      title: 'Create Your First Operator',
      description: 'Initialize a new operator project',
      icon: Rocket,
      duration: '3 minutes',
      content: {
        overview: 'Create a new operator project using the CLI',
        commands: [
          'tixl create hello-world',
          'cd hello-world',
          'npm install',
          'npm run dev'
        ],
        verification: 'You should see a development server running on localhost:3000'
      }
    },
    {
      id: 2,
      title: 'Understand the Structure',
      description: 'Explore project files and architecture',
      icon: Code,
      duration: '5 minutes',
      content: {
        overview: 'Learn about the generated project structure',
        files: [
          { name: 'src/index.ts', description: 'Main operator implementation' },
          { name: 'src/types.ts', description: 'Type definitions and schemas' },
          { name: 'config.json', description: 'Operator configuration' },
          { name: 'package.json', description: 'Project dependencies and metadata' }
        ],
        key_concepts: [
          'Operators are modular units of functionality',
          'Each operator has input/output contracts',
          'Configuration is separate from code',
          'Testing is built into the structure'
        ]
      }
    },
    {
      id: 3,
      title: 'Build Your Logic',
      description: 'Implement operator functionality',
      icon: Settings,
      duration: '10 minutes',
      content: {
        overview: 'Write the core logic for your operator',
        code_example: `import { Operator } from '@tixl/sdk';

class HelloWorldOperator extends Operator {
  async execute(input: { name: string }) {
    const { name } = input;
    
    // Your logic here
    const greeting = \`Hello, \${name}! Welcome to TiXL.\`;
    
    return {
      message: greeting,
      timestamp: new Date().toISOString()
    };
  }
}

export default HelloWorldOperator;`,
        explanation: 'This simple operator takes a name as input and returns a personalized greeting with a timestamp.'
      }
    },
    {
      id: 4,
      title: 'Test Your Operator',
      description: 'Verify functionality with tests',
      icon: Play,
      duration: '5 minutes',
      content: {
        overview: 'Run tests to ensure your operator works correctly',
        test_code: `import { describe, test, expect } from '@tixl/testing';
import HelloWorldOperator from '../src/index';

describe('HelloWorldOperator', () => {
  test('should generate greeting', async () => {
    const operator = new HelloWorldOperator();
    const result = await operator.execute({ name: 'Developer' });
    
    expect(result.message).toBe('Hello, Developer! Welcome to TiXL.');
    expect(result.timestamp).toBeTruthy();
  });
});`,
        commands: [
          'npm test',
          'npm run test:watch', // for continuous testing during development
          'npm run test:coverage' // for coverage report
        ]
      }
    },
    {
      id: 5,
      title: 'Deploy Your Operator',
      description: 'Package and publish your operator',
      icon: CheckCircle,
      duration: '8 minutes',
      content: {
        overview: 'Prepare your operator for distribution',
        steps: [
          'Update package.json with proper metadata',
          'Add comprehensive documentation',
          'Run quality checks (linting, tests)',
          'Package your operator',
          'Publish to the marketplace'
        ],
        commands: [
          'npm run lint',
          'npm run test',
          'npm run build',
          'tixl publish'
        ],
        marketplace: 'Your operator will be available in the TiXL Marketplace after review'
      }
    }
  ];

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="text-center mb-12">
          <h1 className="text-4xl font-bold text-gray-900 mb-4">Quick Start Guide</h1>
          <p className="text-xl text-gray-600 max-w-3xl mx-auto">
            Get up and running with TiXL operators in just 15 minutes. 
            Follow these steps to build, test, and deploy your first operator.
          </p>
        </div>

        {/* Progress Overview */}
        <div className="mb-12">
          <div className="flex items-center justify-between mb-4">
            <span className="text-sm font-medium text-gray-700">Progress</span>
            <span className="text-sm text-gray-500">
              {completedSteps.length} of {steps.length} completed
            </span>
          </div>
          <div className="w-full bg-gray-200 rounded-full h-2">
            <div 
              className="bg-blue-600 h-2 rounded-full transition-all duration-300"
              style={{ width: `${(completedSteps.length / steps.length) * 100}%` }}
            ></div>
          </div>
        </div>

        {/* Steps */}
        <div className="space-y-6">
          {steps.map((step, index) => {
            const Icon = step.icon;
            const isCompleted = completedSteps.includes(step.id);
            const isActive = activeStep === index;

            return (
              <div key={step.id} className="bg-white rounded-lg shadow-sm border border-gray-200">
                {/* Step Header */}
                <button
                  onClick={() => setActiveStep(activeStep === index ? -1 : index)}
                  className="w-full p-6 text-left hover:bg-gray-50 transition-colors"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center space-x-4">
                      <div className={`w-10 h-10 rounded-full flex items-center justify-center ${
                        isCompleted ? 'bg-green-100' : isActive ? 'bg-blue-100' : 'bg-gray-100'
                      }`}>
                        {isCompleted ? (
                          <CheckCircle className="w-6 h-6 text-green-600" />
                        ) : (
                          <Icon className={`w-6 h-6 ${
                            isActive ? 'text-blue-600' : 'text-gray-500'
                          }`} />
                        )}
                      </div>
                      <div>
                        <h3 className="text-lg font-semibold text-gray-900">
                          {index + 1}. {step.title}
                        </h3>
                        <p className="text-gray-600">{step.description}</p>
                      </div>
                    </div>
                    <div className="flex items-center space-x-2">
                      <span className="text-sm text-gray-500">{step.duration}</span>
                      <ArrowRight className={`w-5 h-5 transition-transform ${
                        isActive ? 'rotate-90' : ''
                      } text-gray-400`} />
                    </div>
                  </div>
                </button>

                {/* Step Content */}
                {isActive && (
                  <div className="px-6 pb-6 border-t border-gray-100">
                    <div className="pt-6">
                      <h4 className="text-lg font-semibold text-gray-900 mb-3">
                        {step.content.overview}
                      </h4>

                      {/* Prerequisites */}
                      {step.content.prerequisites && (
                        <div className="mb-6">
                          <h5 className="text-md font-semibold text-gray-800 mb-2">Prerequisites:</h5>
                          <ul className="list-disc list-inside text-gray-600 space-y-1">
                            {step.content.prerequisites.map((prereq, i) => (
                              <li key={i}>{prereq}</li>
                            ))}
                          </ul>
                        </div>
                      )}

                      {/* Commands */}
                      {step.content.commands && (
                        <div className="mb-6">
                          <h5 className="text-md font-semibold text-gray-800 mb-2">Commands:</h5>
                          <div className="bg-gray-900 rounded-lg p-4 font-mono text-sm">
                            {step.content.commands.map((command, i) => (
                              <div key={i} className="text-green-400 mb-1">
                                $ {command}
                              </div>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Code Examples */}
                      {step.content.code_example && (
                        <div className="mb-6">
                          <h5 className="text-md font-semibold text-gray-800 mb-2">Example Code:</h5>
                          <div className="bg-gray-900 rounded-lg p-4 font-mono text-sm overflow-x-auto">
                            <pre className="text-green-400">
                              {step.content.code_example}
                            </pre>
                          </div>
                          {step.content.explanation && (
                            <p className="text-gray-600 mt-2">{step.content.explanation}</p>
                          )}
                        </div>
                      )}

                      {/* Test Code */}
                      {step.content.test_code && (
                        <div className="mb-6">
                          <h5 className="text-md font-semibold text-gray-800 mb-2">Test Example:</h5>
                          <div className="bg-gray-900 rounded-lg p-4 font-mono text-sm overflow-x-auto">
                            <pre className="text-green-400">
                              {step.content.test_code}
                            </pre>
                          </div>
                        </div>
                      )}

                      {/* Files */}
                      {step.content.files && (
                        <div className="mb-6">
                          <h5 className="text-md font-semibold text-gray-800 mb-2">Key Files:</h5>
                          <div className="space-y-2">
                            {step.content.files.map((file, i) => (
                              <div key={i} className="flex items-start space-x-3">
                                <code className="bg-gray-100 px-2 py-1 rounded text-sm">{file.name}</code>
                                <span className="text-gray-600 text-sm">{file.description}</span>
                              </div>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Key Concepts */}
                      {step.content.key_concepts && (
                        <div className="mb-6">
                          <h5 className="text-md font-semibold text-gray-800 mb-2">Key Concepts:</h5>
                          <ul className="list-disc list-inside text-gray-600 space-y-1">
                            {step.content.key_concepts.map((concept, i) => (
                              <li key={i}>{concept}</li>
                            ))}
                          </ul>
                        </div>
                      )}

                      {/* Steps */}
                      {step.content.steps && (
                        <div className="mb-6">
                          <h5 className="text-md font-semibold text-gray-800 mb-2">Steps:</h5>
                          <ol className="list-decimal list-inside text-gray-600 space-y-1">
                            {step.content.steps.map((stepItem, i) => (
                              <li key={i}>{stepItem}</li>
                            ))}
                          </ol>
                        </div>
                      )}

                      {/* Verification */}
                      {step.content.verification && (
                        <div className="mb-4">
                          <h5 className="text-md font-semibold text-gray-800 mb-2">Verification:</h5>
                          <p className="text-gray-600">{step.content.verification}</p>
                        </div>
                      )}

                      {/* Marketplace Info */}
                      {step.content.marketplace && (
                        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                          <p className="text-blue-800">{step.content.marketplace}</p>
                        </div>
                      )}

                      {/* Mark as Complete */}
                      <div className="mt-6 pt-4 border-t border-gray-100">
                        <button
                          onClick={() => toggleStep(step.id)}
                          className={`inline-flex items-center px-4 py-2 border rounded-md text-sm font-medium transition-colors ${
                            isCompleted
                              ? 'border-green-300 text-green-700 bg-green-50 hover:bg-green-100'
                              : 'border-blue-300 text-blue-700 bg-blue-50 hover:bg-blue-100'
                          }`}
                        >
                          {isCompleted ? (
                            <>
                              <CheckCircle className="w-4 h-4 mr-2" />
                              Mark as Incomplete
                            </>
                          ) : (
                            <>
                              <CheckCircle className="w-4 h-4 mr-2" />
                              Mark as Complete
                            </>
                          )}
                        </button>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>

        {/* Next Steps */}
        <div className="mt-12 bg-gradient-to-r from-blue-600 to-purple-600 rounded-lg p-8 text-center">
          <h3 className="text-2xl font-bold text-white mb-4">Congratulations!</h3>
          <p className="text-blue-100 mb-6">
            You've completed the quick start guide. You're now ready to explore 
            advanced topics and build sophisticated TiXL operators.
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <a
              href="/examples"
              className="inline-flex items-center px-6 py-3 border border-transparent text-base font-medium rounded-md text-blue-600 bg-white hover:bg-gray-50 transition-colors"
            >
              <Code className="w-5 h-5 mr-2" />
              View Code Examples
            </a>
            <a
              href="/documentation"
              className="inline-flex items-center px-6 py-3 border border-white text-base font-medium rounded-md text-white hover:bg-white hover:text-blue-600 transition-colors"
            >
              <Code className="w-5 h-5 mr-2" />
              Deep Dive Documentation
            </a>
          </div>
        </div>
      </div>
    </div>
  );
}