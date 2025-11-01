import { Link } from 'react-router-dom';
import { ArrowRight, Code, Rocket, Users, Book, Layers, Heart, Star, Download, Github } from 'lucide-react';

export function Home() {
  const features = [
    {
      icon: Book,
      title: 'Complete API Documentation',
      description: 'Comprehensive documentation covering all aspects of TiXL plugin development with detailed examples.',
      link: '/documentation'
    },
    {
      icon: Rocket,
      title: 'Quick Start Guide',
      description: 'Get up and running in minutes with our step-by-step guide for creating your first operator.',
      link: '/quick-start'
    },
    {
      icon: Code,
      title: 'Code Examples',
      description: 'Real-world examples showcasing different operator types and implementation patterns.',
      link: '/examples'
    },
    {
      icon: Layers,
      title: 'Plugin Marketplace',
      description: 'Discover and publish operators in our curated marketplace for easy distribution.',
      link: '/marketplace'
    },
    {
      icon: Star,
      title: 'Development Tools',
      description: 'Powerful SDK tools for testing, debugging, and validating your plugins.',
      link: '/tools'
    },
    {
      icon: Users,
      title: 'Community Support',
      description: 'Join our active developer community for support, sharing, and collaboration.',
      link: '/community'
    }
  ];

  const stats = [
    { label: 'Available Plugins', value: '2,847+' },
    { label: 'Active Developers', value: '15,632+' },
    { label: 'Downloads', value: '1.2M+' },
    { label: 'Documentation Pages', value: '450+' }
  ];

  return (
    <div className="min-h-screen">
      {/* Hero Section */}
      <section className="bg-gradient-to-br from-blue-50 via-white to-purple-50 py-20">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center">
            <h1 className="text-5xl md:text-6xl font-bold text-gray-900 mb-6">
              Build Amazing
              <span className="text-transparent bg-clip-text bg-gradient-to-r from-blue-600 to-purple-600">
                {' '}TiXL Operators
              </span>
            </h1>
            <p className="text-xl text-gray-600 mb-8 max-w-3xl mx-auto">
              The comprehensive SDK hub for TiXL third-party developers. Create, test, and distribute 
              powerful operators with our complete toolkit and documentation.
            </p>
            <div className="flex flex-col sm:flex-row gap-4 justify-center">
              <Link
                to="/quick-start"
                className="inline-flex items-center px-8 py-3 border border-transparent text-base font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700 transition-colors"
              >
                <Rocket className="w-5 h-5 mr-2" />
                Get Started
                <ArrowRight className="w-5 h-5 ml-2" />
              </Link>
              <Link
                to="/documentation"
                className="inline-flex items-center px-8 py-3 border border-gray-300 text-base font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 transition-colors"
              >
                <Book className="w-5 h-5 mr-2" />
                Read Docs
              </Link>
            </div>
          </div>
        </div>
      </section>

      {/* Stats Section */}
      <section className="py-16 bg-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-8">
            {stats.map((stat, index) => (
              <div key={index} className="text-center">
                <div className="text-3xl font-bold text-blue-600 mb-2">{stat.value}</div>
                <div className="text-gray-600">{stat.label}</div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Features Grid */}
      <section className="py-20 bg-gray-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-16">
            <h2 className="text-3xl md:text-4xl font-bold text-gray-900 mb-4">
              Everything You Need to Build
            </h2>
            <p className="text-xl text-gray-600 max-w-3xl mx-auto">
              Our comprehensive toolkit provides all the resources, tools, and support 
              you need to create world-class TiXL operators.
            </p>
          </div>
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-8">
            {features.map((feature, index) => {
              const Icon = feature.icon;
              return (
                <Link
                  key={index}
                  to={feature.link}
                  className="group p-8 bg-white rounded-xl shadow-sm border border-gray-200 hover:shadow-md hover:border-blue-200 transition-all duration-200"
                >
                  <div className="w-12 h-12 bg-blue-100 rounded-lg flex items-center justify-center mb-4 group-hover:bg-blue-200 transition-colors">
                    <Icon className="w-6 h-6 text-blue-600" />
                  </div>
                  <h3 className="text-xl font-semibold text-gray-900 mb-2">{feature.title}</h3>
                  <p className="text-gray-600 mb-4">{feature.description}</p>
                  <div className="flex items-center text-blue-600 group-hover:text-blue-700">
                    <span className="text-sm font-medium">Learn more</span>
                    <ArrowRight className="w-4 h-4 ml-1 group-hover:translate-x-1 transition-transform" />
                  </div>
                </Link>
              );
            })}
          </div>
        </div>
      </section>

      {/* Quick Start CTA */}
      <section className="py-20 bg-gradient-to-r from-blue-600 to-purple-600">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
          <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">
            Ready to Start Building?
          </h2>
          <p className="text-xl text-blue-100 mb-8 max-w-2xl mx-auto">
            Join thousands of developers creating amazing TiXL operators. 
            Start your journey today with our quick start guide.
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <Link
              to="/quick-start"
              className="inline-flex items-center px-8 py-3 border border-transparent text-base font-medium rounded-md text-blue-600 bg-white hover:bg-gray-50 transition-colors"
            >
              <Rocket className="w-5 h-5 mr-2" />
              Start Building
            </Link>
            <a
              href="https://github.com/tixl"
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center px-8 py-3 border border-white text-base font-medium rounded-md text-white hover:bg-white hover:text-blue-600 transition-colors"
            >
              <Github className="w-5 h-5 mr-2" />
              View on GitHub
            </a>
          </div>
        </div>
      </section>

      {/* Community Section */}
      <section className="py-20 bg-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-16">
            <h2 className="text-3xl md:text-4xl font-bold text-gray-900 mb-4">
              Join Our Community
            </h2>
            <p className="text-xl text-gray-600 max-w-3xl mx-auto">
              Connect with thousands of developers, share your work, and get help 
              from our supportive community.
            </p>
          </div>
          <div className="grid md:grid-cols-3 gap-8">
            <div className="text-center p-6">
              <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
                <Users className="w-8 h-8 text-green-600" />
              </div>
              <h3 className="text-xl font-semibold text-gray-900 mb-2">Developer Forum</h3>
              <p className="text-gray-600 mb-4">Ask questions, share ideas, and get help from experienced developers.</p>
              <Link to="/community" className="text-blue-600 hover:text-blue-700 font-medium">
                Join Discussion →
              </Link>
            </div>
            <div className="text-center p-6">
              <div className="w-16 h-16 bg-purple-100 rounded-full flex items-center justify-center mx-auto mb-4">
                <Heart className="w-8 h-8 text-purple-600" />
              </div>
              <h3 className="text-xl font-semibold text-gray-900 mb-2">Showcase</h3>
              <p className="text-gray-600 mb-4">Share your operators and see what others are building.</p>
              <Link to="/marketplace" className="text-blue-600 hover:text-blue-700 font-medium">
                Explore Gallery →
              </Link>
            </div>
            <div className="text-center p-6">
              <div className="w-16 h-16 bg-blue-100 rounded-full flex items-center justify-center mx-auto mb-4">
                <Download className="w-8 h-8 text-blue-600" />
              </div>
              <h3 className="text-xl font-semibold text-gray-900 mb-2">Resources</h3>
              <p className="text-gray-600 mb-4">Access tools, templates, and resources to accelerate your development.</p>
              <Link to="/tools" className="text-blue-600 hover:text-blue-700 font-medium">
                Get Tools →
              </Link>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}