import { CheckCircle, XCircle, AlertTriangle, Upload, Star, Shield } from 'lucide-react';

export function PublishingGuidelines() {
  const requirements = [
    {
      category: 'Code Quality',
      icon: CheckCircle,
      items: [
        { text: 'Code passes all tests and has >90% test coverage', required: true },
        { text: 'No TypeScript or ESLint errors or warnings', required: true },
        { text: 'Follows naming conventions and style guide', required: true },
        { text: 'Documentation is up-to-date and comprehensive', required: true },
        { text: 'Performance optimized for expected workloads', required: true }
      ]
    },
    {
      category: 'Security',
      icon: Shield,
      items: [
        { text: 'No hardcoded credentials or sensitive data', required: true },
        { text: 'Input validation and sanitization implemented', required: true },
        { text: 'Secure dependencies (no known vulnerabilities)', required: true },
        { text: 'Proper error handling without information leakage', required: true },
        { text: 'Follows security best practices', required: true }
      ]
    },
    {
      category: 'Functionality',
      icon: Star,
      items: [
        { text: 'Operator does what it promises in description', required: true },
        { text: 'Handles edge cases and error conditions gracefully', required: true },
        { text: 'Documentation includes usage examples', required: true },
        { text: 'Version number follows semantic versioning', required: true },
        { text: 'Compatible with current TiXL SDK version', required: true }
      ]
    }
  ];

  const rejectionReasons = [
    {
      reason: 'Incomplete Documentation',
      description: 'Missing or insufficient documentation explaining functionality and usage',
      icon: XCircle,
      severity: 'high'
    },
    {
      reason: 'Poor Code Quality',
      description: 'Code does not meet quality standards, has bugs, or poor performance',
      icon: XCircle,
      severity: 'high'
    },
    {
      reason: 'Security Issues',
      description: 'Contains security vulnerabilities, hardcoded secrets, or unsafe practices',
      icon: XCircle,
      severity: 'critical'
    },
    {
      reason: 'Duplicate Functionality',
      description: 'Provides the same functionality as existing operators without significant improvement',
      icon: AlertTriangle,
      severity: 'medium'
    },
    {
      reason: 'Testing Issues',
      description: 'Failing tests, insufficient test coverage, or untested edge cases',
      icon: XCircle,
      severity: 'high'
    }
  ];

  const processSteps = [
    {
      step: 1,
      title: 'Prepare Your Operator',
      description: 'Ensure your operator meets all requirements and quality standards',
      duration: 'Variable',
      details: [
        'Run all tests and fix any issues',
        'Update documentation and README',
        'Update version number following semantic versioning',
        'Run quality checks (linting, security scans)'
      ]
    },
    {
      step: 2,
      title: 'Package and Test',
      description: 'Create a production-ready package for distribution',
      duration: '5-10 minutes',
      details: [
        'Build the operator for production',
        'Test the packaged operator locally',
        'Verify all dependencies are properly included',
        'Create a changelog for the version'
      ]
    },
    {
      step: 3,
      title: 'Submit for Review',
      description: 'Submit your operator through the CLI or web interface',
      duration: '2 minutes',
      details: [
        'Use `tixl publish` command',
        'Provide detailed description and changelog',
        'Include screenshots or demos if applicable',
        'Specify appropriate categories and tags'
      ]
    },
    {
      step: 4,
      title: 'Review Process',
      description: 'TiXL team reviews your submission for quality and compliance',
      duration: '1-3 business days',
      details: [
        'Automated checks for code quality and security',
        'Manual review by TiXL team members',
        'Testing on staging environment',
        'Feedback and revision requests if needed'
      ]
    },
    {
      step: 5,
      title: 'Publication',
      description: 'Approved operators are published to the marketplace',
      duration: 'Immediate',
      details: [
        'Operator becomes publicly available',
        'Added to search and discovery features',
        'Receives official TiXL seal of approval',
        'Notifications sent to followers'
      ]
    }
  ];

  const publishCommand = `tixl publish --package-path ./dist --description "Your operator description" --tags "data,api,processing" --category "Data Processing"`;

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="text-center mb-12">
          <h1 className="text-4xl font-bold text-gray-900 mb-4">Publishing Guidelines</h1>
          <p className="text-xl text-gray-600 max-w-3xl mx-auto">
            Learn the requirements and best practices for publishing high-quality 
            operators to the TiXL Marketplace.
          </p>
        </div>

        {/* Quick Reference */}
        <div className="grid md:grid-cols-3 gap-6 mb-12">
          <div className="bg-green-50 border border-green-200 rounded-lg p-6 text-center">
            <CheckCircle className="w-12 h-12 text-green-600 mx-auto mb-4" />
            <h3 className="text-lg font-semibold text-green-900 mb-2">Quality Standards</h3>
            <p className="text-green-700 text-sm">Maintain high code quality and comprehensive testing</p>
          </div>
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-6 text-center">
            <Shield className="w-12 h-12 text-blue-600 mx-auto mb-4" />
            <h3 className="text-lg font-semibold text-blue-900 mb-2">Security First</h3>
            <p className="text-blue-700 text-sm">Implement robust security practices and validation</p>
          </div>
          <div className="bg-purple-50 border border-purple-200 rounded-lg p-6 text-center">
            <Star className="w-12 h-12 text-purple-600 mx-auto mb-4" />
            <h3 className="text-lg font-semibold text-purple-900 mb-2">User Experience</h3>
            <p className="text-purple-700 text-sm">Provide excellent documentation and ease of use</p>
          </div>
        </div>

        {/* Requirements */}
        <div className="mb-12">
          <h2 className="text-3xl font-bold text-gray-900 mb-8">Requirements Checklist</h2>
          <div className="space-y-6">
            {requirements.map((requirement, index) => {
              const Icon = requirement.icon;
              return (
                <div key={index} className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                  <div className="flex items-center space-x-3 mb-4">
                    <Icon className="w-6 h-6 text-blue-600" />
                    <h3 className="text-xl font-semibold text-gray-900">{requirement.category}</h3>
                  </div>
                  <div className="grid md:grid-cols-2 gap-4">
                    {requirement.items.map((item, itemIndex) => (
                      <div key={itemIndex} className="flex items-start space-x-3">
                        <CheckCircle className={`w-5 h-5 mt-0.5 ${item.required ? 'text-green-500' : 'text-gray-300'}`} />
                        <span className={`${item.required ? 'text-gray-900' : 'text-gray-600'}`}>
                          {item.text}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Publishing Process */}
        <div className="mb-12">
          <h2 className="text-3xl font-bold text-gray-900 mb-8">Publishing Process</h2>
          <div className="space-y-6">
            {processSteps.map((process, index) => (
              <div key={index} className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <div className="flex items-start space-x-4">
                  <div className="w-8 h-8 bg-blue-600 text-white rounded-full flex items-center justify-center font-semibold text-sm">
                    {process.step}
                  </div>
                  <div className="flex-1">
                    <div className="flex items-center justify-between mb-2">
                      <h3 className="text-lg font-semibold text-gray-900">{process.title}</h3>
                      <span className="text-sm text-gray-500">{process.duration}</span>
                    </div>
                    <p className="text-gray-600 mb-4">{process.description}</p>
                    <ul className="space-y-1">
                      {process.details.map((detail, detailIndex) => (
                        <li key={detailIndex} className="flex items-center space-x-2 text-sm text-gray-600">
                          <div className="w-1.5 h-1.5 bg-blue-500 rounded-full"></div>
                          <span>{detail}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* CLI Command */}
        <div className="mb-12">
          <h2 className="text-3xl font-bold text-gray-900 mb-8">Publishing via CLI</h2>
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h3 className="text-lg font-semibold text-gray-900 mb-4">Quick Publish Command</h3>
            <div className="bg-gray-900 rounded-lg p-4 text-green-400 font-mono text-sm mb-4">
              {publishCommand}
            </div>
            <div className="space-y-4">
              <div>
                <h4 className="font-semibold text-gray-900 mb-2">Parameters:</h4>
                <ul className="space-y-1 text-sm text-gray-600">
                  <li><code className="bg-gray-100 px-1 rounded">--package-path</code> - Path to the built operator package</li>
                  <li><code className="bg-gray-100 px-1 rounded">--description</code> - Description of the operator functionality</li>
                  <li><code className="bg-gray-100 px-1 rounded">--tags</code> - Comma-separated list of tags</li>
                  <li><code className="bg-gray-100 px-1 rounded">--category</code> - Primary category for the operator</li>
                </ul>
              </div>
              <div>
                <h4 className="font-semibold text-gray-900 mb-2">After Publishing:</h4>
                <ul className="space-y-1 text-sm text-gray-600">
                  <li>• You'll receive a confirmation email with submission details</li>
                  <li>• The review process typically takes 1-3 business days</li>
                  <li>• You'll be notified of approval or if revisions are needed</li>
                  <li>• Once approved, your operator becomes publicly available</li>
                </ul>
              </div>
            </div>
          </div>
        </div>

        {/* Rejection Reasons */}
        <div className="mb-12">
          <h2 className="text-3xl font-bold text-gray-900 mb-8">Common Rejection Reasons</h2>
          <div className="space-y-4">
            {rejectionReasons.map((reason, index) => {
              const Icon = reason.icon;
              return (
                <div key={index} className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                  <div className="flex items-start space-x-4">
                    <Icon className={`w-6 h-6 mt-1 ${
                      reason.severity === 'critical' ? 'text-red-500' :
                      reason.severity === 'high' ? 'text-orange-500' : 'text-yellow-500'
                    }`} />
                    <div className="flex-1">
                      <div className="flex items-center space-x-2 mb-2">
                        <h3 className="text-lg font-semibold text-gray-900">{reason.reason}</h3>
                        <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                          reason.severity === 'critical' ? 'bg-red-100 text-red-800' :
                          reason.severity === 'high' ? 'bg-orange-100 text-orange-800' : 'bg-yellow-100 text-yellow-800'
                        }`}>
                          {reason.severity}
                        </span>
                      </div>
                      <p className="text-gray-600">{reason.description}</p>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Tips for Success */}
        <div className="bg-gradient-to-r from-blue-600 to-purple-600 rounded-lg p-8 text-white">
          <h2 className="text-2xl font-bold mb-6">Tips for Successful Publishing</h2>
          <div className="grid md:grid-cols-2 gap-6">
            <div>
              <h3 className="text-lg font-semibold mb-3">Before Submission</h3>
              <ul className="space-y-2 text-blue-100">
                <li>• Test thoroughly on different environments</li>
                <li>• Get feedback from other developers</li>
                <li>• Read the documentation multiple times</li>
                <li>• Use the CLI tools to validate your package</li>
              </ul>
            </div>
            <div>
              <h3 className="text-lg font-semibold mb-3">After Approval</h3>
              <ul className="space-y-2 text-blue-100">
                <li>• Respond to user feedback quickly</li>
                <li>• Keep your operator updated</li>
                <li>• Monitor performance and usage</li>
                <li>• Promote your operator in the community</li>
              </ul>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}