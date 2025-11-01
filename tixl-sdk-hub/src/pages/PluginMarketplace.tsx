import { useState } from 'react';
import { Search, Filter, Star, Download, Heart, Eye, ExternalLink, Tag, TrendingUp } from 'lucide-react';

export function PluginMarketplace() {
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('all');
  const [sortBy, setSortBy] = useState('popular');

  const categories = [
    { id: 'all', name: 'All Categories', count: 2847 },
    { id: 'data-processing', name: 'Data Processing', count: 432 },
    { id: 'api-integration', name: 'API Integration', count: 398 },
    { id: 'transformation', name: 'Data Transformation', count: 276 },
    { id: 'analytics', name: 'Analytics & ML', count: 189 },
    { id: 'storage', name: 'Storage & DB', count: 167 },
    { id: 'messaging', name: 'Messaging', count: 134 },
    { id: 'security', name: 'Security', count: 98 }
  ];

  const featuredPlugins = [
    {
      id: 'dataflow-processor',
      name: 'DataFlow Processor',
      description: 'High-performance data processing pipeline with parallel execution and memory optimization',
      category: 'data-processing',
      author: 'DataFlow Labs',
      version: '2.4.1',
      rating: 4.9,
      downloads: 45632,
      price: 'Free',
      tags: ['pipeline', 'performance', 'parallel'],
      featured: true,
      verified: true
    },
    {
      id: 'api-gateway',
      name: 'API Gateway',
      description: 'Universal API gateway with rate limiting, authentication, and monitoring capabilities',
      category: 'api-integration',
      author: 'Gateway Solutions',
      version: '3.1.0',
      rating: 4.8,
      downloads: 32145,
      price: 'Free',
      tags: ['gateway', 'auth', 'monitoring'],
      featured: true,
      verified: true
    },
    {
      id: 'ai-optimizer',
      name: 'AI Model Optimizer',
      description: 'Automatically optimize and deploy machine learning models for production',
      category: 'analytics',
      author: 'AI Works',
      version: '1.8.2',
      rating: 4.7,
      downloads: 18754,
      price: '$49/month',
      tags: ['ml', 'optimization', 'deployment'],
      featured: true,
      verified: true
    }
  ];

  const allPlugins = [
    // ... (would include more plugins in real implementation)
  ];

  const recentPlugins = [
    {
      id: 'crypto-parser',
      name: 'Cryptocurrency Parser',
      description: 'Real-time cryptocurrency data parsing and normalization',
      category: 'api-integration',
      author: 'CryptoDev',
      version: '1.0.0',
      rating: 4.5,
      downloads: 1234,
      price: 'Free',
      tags: ['crypto', 'realtime', 'parsing'],
      verified: false,
      publishedAt: '2025-11-01'
    },
    {
      id: 'image-processor',
      name: 'Image Processor',
      description: 'Advanced image processing with multiple filters and transformations',
      category: 'data-processing',
      author: 'ImageTech',
      version: '2.1.3',
      rating: 4.6,
      downloads: 5432,
      price: 'Free',
      tags: ['image', 'filters', 'transformation'],
      verified: true,
      publishedAt: '2025-10-28'
    }
  ];

  const stats = [
    { label: 'Total Plugins', value: '2,847', change: '+12%' },
    { label: 'Downloads', value: '1.2M', change: '+8%' },
    { label: 'Active Developers', value: '1,432', change: '+15%' },
    { label: 'Revenue Share', value: '$847K', change: '+23%' }
  ];

  const filteredPlugins = [...featuredPlugins, ...recentPlugins].filter(plugin => {
    const matchesSearch = plugin.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         plugin.description.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         plugin.tags.some(tag => tag.toLowerCase().includes(searchTerm.toLowerCase()));
    const matchesCategory = selectedCategory === 'all' || plugin.category === selectedCategory;
    return matchesSearch && matchesCategory;
  });

  const sortedPlugins = [...filteredPlugins].sort((a, b) => {
    switch (sortBy) {
      case 'popular': return b.downloads - a.downloads;
      case 'rating': return b.rating - a.rating;
      case 'newest': return new Date((b as any).publishedAt || '2025-11-01').getTime() - new Date((a as any).publishedAt || '2025-10-01').getTime();
      case 'name': return a.name.localeCompare(b.name);
      default: return 0;
    }
  });

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="text-center mb-12">
          <h1 className="text-4xl font-bold text-gray-900 mb-4">Plugin Marketplace</h1>
          <p className="text-xl text-gray-600 max-w-3xl mx-auto">
            Discover powerful TiXL operators created by our community. 
            Find the perfect plugins to enhance your workflows.
          </p>
        </div>

        {/* Stats */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-12">
          {stats.map((stat, index) => (
            <div key={index} className="bg-white rounded-lg p-6 text-center shadow-sm border border-gray-200">
              <div className="text-2xl font-bold text-gray-900 mb-1">{stat.value}</div>
              <div className="text-gray-600 text-sm mb-1">{stat.label}</div>
              <div className="text-green-600 text-xs font-medium">{stat.change} this month</div>
            </div>
          ))}
        </div>

        {/* Search and Filters */}
        <div className="mb-8">
          <div className="flex flex-col lg:flex-row gap-4 mb-6">
            {/* Search */}
            <div className="flex-1">
              <div className="relative">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-5 h-5" />
                <input
                  type="text"
                  placeholder="Search plugins..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                />
              </div>
            </div>
            
            {/* Sort */}
            <select
              value={sortBy}
              onChange={(e) => setSortBy(e.target.value)}
              className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            >
              <option value="popular">Most Popular</option>
              <option value="rating">Highest Rated</option>
              <option value="newest">Newest</option>
              <option value="name">Name A-Z</option>
            </select>
          </div>

          {/* Categories */}
          <div className="flex flex-wrap gap-2">
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
                {category.name} ({category.count})
              </button>
            ))}
          </div>
        </div>

        {/* Featured Plugins */}
        {!searchTerm && selectedCategory === 'all' && (
          <div className="mb-12">
            <h2 className="text-2xl font-bold text-gray-900 mb-6">Featured Plugins</h2>
            <div className="grid lg:grid-cols-3 gap-6">
              {featuredPlugins.map((plugin) => (
                <PluginCard key={plugin.id} plugin={plugin} featured />
              ))}
            </div>
          </div>
        )}

        {/* All Plugins */}
        <div className="mb-8">
          <div className="flex items-center justify-between mb-6">
            <h2 className="text-2xl font-bold text-gray-900">
              {searchTerm || selectedCategory !== 'all' ? 'Search Results' : 'All Plugins'}
            </h2>
            <span className="text-gray-600">{sortedPlugins.length} plugins found</span>
          </div>
          
          <div className="grid lg:grid-cols-2 xl:grid-cols-3 gap-6">
            {sortedPlugins.map((plugin) => (
              <PluginCard key={plugin.id} plugin={plugin} />
            ))}
          </div>
        </div>

        {/* Submit Plugin CTA */}
        <div className="bg-gradient-to-r from-blue-600 to-purple-600 rounded-lg p-8 text-center text-white">
          <h2 className="text-2xl font-bold mb-4">Share Your Plugin</h2>
          <p className="text-blue-100 mb-6 max-w-2xl mx-auto">
            Have an amazing TiXL operator? Share it with the community and help others 
            build powerful workflows.
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <a
              href="/publishing"
              className="inline-flex items-center px-6 py-3 border border-transparent text-base font-medium rounded-md text-blue-600 bg-white hover:bg-gray-50 transition-colors"
            >
              <ExternalLink className="w-5 h-5 mr-2" />
              Submit Plugin
            </a>
            <a
              href="/quick-start"
              className="inline-flex items-center px-6 py-3 border border-white text-base font-medium rounded-md text-white hover:bg-white hover:text-blue-600 transition-colors"
            >
              Build Plugin
            </a>
          </div>
        </div>
      </div>
    </div>
  );
}

function PluginCard({ plugin, featured = false }) {
  return (
    <div className={`bg-white rounded-lg shadow-sm border hover:shadow-md transition-shadow ${
      plugin.verified ? 'border-green-200' : 'border-gray-200'
    }`}>
      <div className="p-6">
        {/* Header */}
        <div className="flex items-start justify-between mb-3">
          <div className="flex items-center space-x-2">
            <h3 className="text-lg font-semibold text-gray-900">{plugin.name}</h3>
            {plugin.verified && (
              <div className="w-4 h-4 bg-green-500 rounded-full flex items-center justify-center" title="Verified">
                <svg className="w-2.5 h-2.5 text-white" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                </svg>
              </div>
            )}
          </div>
          {featured && (
            <span className="px-2 py-1 bg-yellow-100 text-yellow-800 text-xs font-medium rounded-full">
              Featured
            </span>
          )}
        </div>

        {/* Description */}
        <p className="text-gray-600 text-sm mb-4 line-clamp-2">{plugin.description}</p>

        {/* Tags */}
        <div className="flex flex-wrap gap-1 mb-4">
          {plugin.tags.slice(0, 3).map((tag, index) => (
            <span key={index} className="px-2 py-1 bg-gray-100 text-gray-700 text-xs rounded">
              {tag}
            </span>
          ))}
          {plugin.tags.length > 3 && (
            <span className="px-2 py-1 bg-gray-100 text-gray-700 text-xs rounded">
              +{plugin.tags.length - 3}
            </span>
          )}
        </div>

        {/* Author */}
        <div className="text-sm text-gray-600 mb-4">by {plugin.author}</div>

        {/* Stats */}
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center space-x-4 text-sm text-gray-600">
            <div className="flex items-center space-x-1">
              <Star className="w-4 h-4 text-yellow-500" />
              <span>{plugin.rating}</span>
            </div>
            <div className="flex items-center space-x-1">
              <Download className="w-4 h-4" />
              <span>{plugin.downloads.toLocaleString()}</span>
            </div>
          </div>
          <div className="text-sm font-medium text-gray-900">{plugin.price}</div>
        </div>

        {/* Actions */}
        <div className="flex space-x-2">
          <button className="flex-1 bg-blue-600 text-white px-4 py-2 rounded-md text-sm font-medium hover:bg-blue-700 transition-colors">
            Install
          </button>
          <button className="px-3 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors">
            <Heart className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
}