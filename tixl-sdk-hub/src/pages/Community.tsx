import { useState } from 'react';
import { Users, MessageCircle, Github, BookOpen, Calendar, Heart, Star, ExternalLink, Mail, Slack, Twitter } from 'lucide-react';

export function Community() {
  const [activeSection, setActiveSection] = useState('forums');

  const communityStats = [
    { label: 'Active Members', value: '15,632', growth: '+12%' },
    { label: 'Forum Posts', value: '8,427', growth: '+8%' },
    { label: 'GitHub Stars', value: '3,245', growth: '+15%' },
    { label: 'Discord Members', value: '2,156', growth: '+23%' }
  ];

  const channels = [
    {
      id: 'forums',
      name: 'Developer Forums',
      description: 'Discuss development topics, ask questions, and share knowledge',
      icon: MessageCircle,
      members: '8,427',
      activity: 'High',
      link: '#',
      color: 'blue'
    },
    {
      id: 'github',
      name: 'GitHub Community',
      description: 'Collaborate on open source projects and contribute to the ecosystem',
      icon: Github,
      members: '3,245',
      activity: 'Very High',
      link: '#',
      color: 'gray'
    },
    {
      id: 'discord',
      name: 'Discord Chat',
      description: 'Real-time chat for quick questions and community discussions',
      icon: MessageCircle,
      members: '2,156',
      activity: 'Very High',
      link: '#',
      color: 'purple'
    },
    {
      id: 'slack',
      name: 'Slack Workspace',
      description: 'Professional networking and team collaboration',
      icon: Slack,
      members: '1,843',
      activity: 'Medium',
      link: '#',
      color: 'green'
    }
  ];

  const events = [
    {
      id: 1,
      title: 'TiXL Developer Summit 2025',
      type: 'Conference',
      date: '2025-12-15',
      location: 'San Francisco, CA',
      status: 'Upcoming',
      description: 'Annual gathering of TiXL developers for networking, workshops, and announcements',
      attendees: 500
    },
    {
      id: 2,
      title: 'Operator Hackathon',
      type: 'Hackathon',
      date: '2025-11-20',
      location: 'Virtual',
      status: 'Registration Open',
      description: 'Build innovative operators in a 48-hour coding marathon',
      attendees: 150
    },
    {
      id: 3,
      title: 'Advanced Patterns Workshop',
      type: 'Workshop',
      date: '2025-11-10',
      location: 'New York, NY',
      status: 'Upcoming',
      description: 'Deep dive into advanced integration patterns and best practices',
      attendees: 75
    },
    {
      id: 4,
      title: 'Monthly Community Call',
      type: 'Meetup',
      date: '2025-11-05',
      location: 'Virtual',
      status: 'Weekly',
      description: 'Regular community updates and Q&A sessions',
      attendees: 200
    }
  ];

  const contributions = [
    {
      id: 1,
      title: 'Documentation Translation Project',
      description: 'Help translate TiXL documentation into multiple languages',
      category: 'Documentation',
      difficulty: 'Beginner',
      participants: 12,
      status: 'Active'
    },
    {
      id: 2,
      title: 'Operator Template Library',
      description: 'Create and maintain a library of common operator templates',
      category: 'Development',
      difficulty: 'Intermediate',
      participants: 8,
      status: 'Active'
    },
    {
      id: 3,
      title: 'Community Blog Content',
      description: 'Write tutorials and articles about TiXL development',
      category: 'Content',
      difficulty: 'Beginner',
      participants: 15,
      status: 'Active'
    },
    {
      id: 4,
      title: 'Testing Framework Enhancement',
      description: 'Improve the TiXL testing framework based on community feedback',
      category: 'Development',
      difficulty: 'Advanced',
      participants: 5,
      status: 'Planning'
    }
  ];

  const resources = [
    {
      title: 'Developer Newsletter',
      description: 'Monthly newsletter with updates, tutorials, and community highlights',
      type: 'Newsletter',
      frequency: 'Monthly',
      icon: Mail
    },
    {
      title: 'Community Blog',
      description: 'Articles, tutorials, and case studies from community members',
      type: 'Blog',
      frequency: 'Weekly',
      icon: BookOpen
    },
    {
      title: 'Office Hours',
      description: 'Weekly live sessions with TiXL team members',
      type: 'Live Session',
      frequency: 'Weekly',
      icon: Users
    },
    {
      title: 'Podcast',
      description: 'Conversations with developers and industry experts',
      type: 'Podcast',
      frequency: 'Bi-weekly',
      icon: MessageCircle
    }
  ];

  const mentors = [
    {
      name: 'Sarah Chen',
      role: 'Senior Developer Advocate',
      expertise: ['API Design', 'Performance Optimization', 'Best Practices'],
      availability: 'Available',
      timezone: 'UTC-8 (PST)'
    },
    {
      name: 'Michael Rodriguez',
      role: 'Community Manager',
      expertise: ['Onboarding', 'Documentation', 'Community Building'],
      availability: 'Available',
      timezone: 'UTC-5 (EST)'
    },
    {
      name: 'Alex Kumar',
      role: 'Technical Lead',
      expertise: ['Architecture', 'Integration Patterns', 'Scaling'],
      availability: 'Busy',
      timezone: 'UTC+1 (CET)'
    }
  ];

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="text-center mb-12">
          <h1 className="text-4xl font-bold text-gray-900 mb-4">Community</h1>
          <p className="text-xl text-gray-600 max-w-3xl mx-auto">
            Join our vibrant community of developers, share your knowledge, 
            get help, and collaborate on exciting projects.
          </p>
        </div>

        {/* Community Stats */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-12">
          {communityStats.map((stat, index) => (
            <div key={index} className="bg-white rounded-lg p-6 text-center shadow-sm border border-gray-200">
              <div className="text-2xl font-bold text-gray-900 mb-1">{stat.value}</div>
              <div className="text-gray-600 text-sm mb-1">{stat.label}</div>
              <div className="text-green-600 text-xs font-medium">{stat.growth}</div>
            </div>
          ))}
        </div>

        {/* Community Channels */}
        <div className="mb-12">
          <h2 className="text-2xl font-bold text-gray-900 mb-8">Join the Conversation</h2>
          <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-6">
            {channels.map((channel) => {
              const Icon = channel.icon;
              return (
                <a
                  key={channel.id}
                  href={channel.link}
                  className="bg-white rounded-lg p-6 shadow-sm border border-gray-200 hover:shadow-md transition-shadow"
                >
                  <div className={`w-12 h-12 bg-${channel.color}-100 rounded-lg flex items-center justify-center mb-4`}>
                    <Icon className={`w-6 h-6 text-${channel.color}-600`} />
                  </div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-2">{channel.name}</h3>
                  <p className="text-gray-600 text-sm mb-4">{channel.description}</p>
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-gray-500">{channel.members} members</span>
                    <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                      channel.activity === 'Very High' ? 'bg-green-100 text-green-800' :
                      channel.activity === 'High' ? 'bg-yellow-100 text-yellow-800' :
                      'bg-blue-100 text-blue-800'
                    }`}>
                      {channel.activity}
                    </span>
                  </div>
                </a>
              );
            })}
          </div>
        </div>

        {/* Section Navigation */}
        <div className="mb-8">
          <div className="flex flex-wrap gap-2 justify-center">
            {[
              { id: 'forums', label: 'Discussion Forums' },
              { id: 'events', label: 'Events & Meetups' },
              { id: 'contributions', label: 'Community Projects' },
              { id: 'resources', label: 'Resources & Learning' },
              { id: 'mentorship', label: 'Mentorship' }
            ].map((section) => (
              <button
                key={section.id}
                onClick={() => setActiveSection(section.id)}
                className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                  activeSection === section.id
                    ? 'bg-blue-600 text-white'
                    : 'bg-white text-gray-700 hover:bg-gray-50 border border-gray-300'
                }`}
              >
                {section.label}
              </button>
            ))}
          </div>
        </div>

        {/* Forums */}
        {activeSection === 'forums' && (
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-8 mb-12">
            <h2 className="text-2xl font-bold text-gray-900 mb-6">Discussion Forums</h2>
            <div className="grid lg:grid-cols-2 gap-6">
              <div className="space-y-4">
                <h3 className="text-lg font-semibold text-gray-900">Popular Topics</h3>
                {[
                  { title: 'Best practices for error handling', replies: 24, activity: '2h ago', category: 'Best Practices' },
                  { title: 'How to optimize operator performance', replies: 18, activity: '4h ago', category: 'Performance' },
                  { title: 'Integration with external APIs', replies: 31, activity: '6h ago', category: 'Integration' },
                  { title: 'Testing strategies for operators', replies: 15, activity: '1d ago', category: 'Testing' }
                ].map((topic, index) => (
                  <div key={index} className="border border-gray-200 rounded-lg p-4 hover:bg-gray-50 cursor-pointer">
                    <h4 className="font-medium text-gray-900 mb-2">{topic.title}</h4>
                    <div className="flex items-center justify-between text-sm text-gray-600">
                      <div className="flex items-center space-x-4">
                        <span>{topic.replies} replies</span>
                        <span>{topic.activity}</span>
                      </div>
                      <span className="px-2 py-1 bg-blue-100 text-blue-800 rounded text-xs">{topic.category}</span>
                    </div>
                  </div>
                ))}
              </div>
              <div className="space-y-4">
                <h3 className="text-lg font-semibold text-gray-900">Featured Discussions</h3>
                {[
                  { title: 'Operator Marketplace Guidelines Update', author: 'TiXL Team', activity: '1d ago' },
                  { title: 'Community Showcase: Amazing Operators', author: 'Community', activity: '2d ago' },
                  { title: 'Q&A with TiXL Core Team', author: 'TiXL Team', activity: '3d ago' }
                ].map((discussion, index) => (
                  <div key={index} className="border border-gray-200 rounded-lg p-4 hover:bg-gray-50 cursor-pointer">
                    <h4 className="font-medium text-gray-900 mb-2">{discussion.title}</h4>
                    <div className="flex items-center justify-between text-sm text-gray-600">
                      <span>by {discussion.author}</span>
                      <span>{discussion.activity}</span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* Events */}
        {activeSection === 'events' && (
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-8 mb-12">
            <h2 className="text-2xl font-bold text-gray-900 mb-6">Events & Meetups</h2>
            <div className="space-y-6">
              {events.map((event) => (
                <div key={event.id} className="border border-gray-200 rounded-lg p-6">
                  <div className="flex items-start justify-between mb-4">
                    <div>
                      <h3 className="text-lg font-semibold text-gray-900 mb-2">{event.title}</h3>
                      <p className="text-gray-600 mb-3">{event.description}</p>
                      <div className="flex items-center space-x-4 text-sm text-gray-600">
                        <span className="flex items-center space-x-1">
                          <Calendar className="w-4 h-4" />
                          <span>{new Date(event.date).toLocaleDateString()}</span>
                        </span>
                        <span>{event.location}</span>
                        <span>{event.attendees} expected attendees</span>
                      </div>
                    </div>
                    <div className="text-right">
                      <span className={`px-3 py-1 rounded-full text-sm font-medium ${
                        event.status === 'Upcoming' ? 'bg-green-100 text-green-800' :
                        event.status === 'Registration Open' ? 'bg-blue-100 text-blue-800' :
                        event.status === 'Weekly' ? 'bg-purple-100 text-purple-800' :
                        'bg-yellow-100 text-yellow-800'
                      }`}>
                        {event.status}
                      </span>
                      <div className="mt-2 text-sm text-gray-600">{event.type}</div>
                    </div>
                  </div>
                  <div className="flex space-x-3">
                    <button className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors">
                      Learn More
                    </button>
                    {event.status === 'Registration Open' && (
                      <button className="px-4 py-2 border border-blue-600 text-blue-600 rounded-md hover:bg-blue-50 transition-colors">
                        Register
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Community Contributions */}
        {activeSection === 'contributions' && (
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-8 mb-12">
            <h2 className="text-2xl font-bold text-gray-900 mb-6">Community Projects</h2>
            <div className="grid lg:grid-cols-2 gap-6">
              {contributions.map((project) => (
                <div key={project.id} className="border border-gray-200 rounded-lg p-6">
                  <div className="flex items-start justify-between mb-4">
                    <div>
                      <h3 className="text-lg font-semibold text-gray-900 mb-2">{project.title}</h3>
                      <p className="text-gray-600 mb-3">{project.description}</p>
                      <div className="flex items-center space-x-4 text-sm text-gray-600">
                        <span>{project.participants} participants</span>
                        <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                          project.status === 'Active' ? 'bg-green-100 text-green-800' : 'bg-yellow-100 text-yellow-800'
                        }`}>
                          {project.status}
                        </span>
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="px-2 py-1 bg-gray-100 text-gray-700 rounded text-sm">{project.category}</span>
                    <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                      project.difficulty === 'Beginner' ? 'bg-green-100 text-green-800' :
                      project.difficulty === 'Intermediate' ? 'bg-yellow-100 text-yellow-800' :
                      'bg-red-100 text-red-800'
                    }`}>
                      {project.difficulty}
                    </span>
                  </div>
                  <div className="mt-4">
                    <button className="w-full px-4 py-2 border border-gray-300 text-gray-700 rounded-md hover:bg-gray-50 transition-colors">
                      Join Project
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Resources */}
        {activeSection === 'resources' && (
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-8 mb-12">
            <h2 className="text-2xl font-bold text-gray-900 mb-6">Community Resources</h2>
            <div className="grid md:grid-cols-2 gap-6">
              {resources.map((resource, index) => {
                const Icon = resource.icon;
                return (
                  <div key={index} className="border border-gray-200 rounded-lg p-6">
                    <div className="flex items-center space-x-3 mb-4">
                      <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
                        <Icon className="w-5 h-5 text-blue-600" />
                      </div>
                      <div>
                        <h3 className="text-lg font-semibold text-gray-900">{resource.title}</h3>
                        <p className="text-gray-600 text-sm">{resource.description}</p>
                      </div>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-sm text-gray-600">{resource.type}</span>
                      <span className="text-sm text-gray-600">{resource.frequency}</span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Mentorship */}
        {activeSection === 'mentorship' && (
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-8 mb-12">
            <h2 className="text-2xl font-bold text-gray-900 mb-6">Mentorship Program</h2>
            <p className="text-gray-600 mb-8">
              Get personalized guidance from experienced TiXL developers. Our mentorship program 
              connects newcomers with experts to accelerate your learning journey.
            </p>
            <div className="grid lg:grid-cols-3 gap-6 mb-8">
              {mentors.map((mentor, index) => (
                <div key={index} className="border border-gray-200 rounded-lg p-6">
                  <div className="flex items-center space-x-3 mb-4">
                    <div className="w-12 h-12 bg-blue-100 rounded-full flex items-center justify-center">
                      <Users className="w-6 h-6 text-blue-600" />
                    </div>
                    <div>
                      <h3 className="text-lg font-semibold text-gray-900">{mentor.name}</h3>
                      <p className="text-gray-600 text-sm">{mentor.role}</p>
                    </div>
                  </div>
                  <div className="mb-4">
                    <h4 className="text-sm font-semibold text-gray-900 mb-2">Expertise:</h4>
                    <div className="flex flex-wrap gap-1">
                      {mentor.expertise.map((skill, skillIndex) => (
                        <span key={skillIndex} className="px-2 py-1 bg-blue-100 text-blue-800 rounded text-xs">
                          {skill}
                        </span>
                      ))}
                    </div>
                  </div>
                  <div className="space-y-2 text-sm text-gray-600 mb-4">
                    <div>Status: <span className={`font-medium ${
                      mentor.availability === 'Available' ? 'text-green-600' : 'text-yellow-600'
                    }`}>{mentor.availability}</span></div>
                    <div>Timezone: {mentor.timezone}</div>
                  </div>
                  <button className="w-full px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors">
                    Request Mentorship
                  </button>
                </div>
              ))}
            </div>
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-6 text-center">
              <h3 className="text-lg font-semibold text-blue-900 mb-2">Become a Mentor</h3>
              <p className="text-blue-700 mb-4">
                Share your knowledge and help new developers grow in the TiXL ecosystem
              </p>
              <button className="px-6 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors">
                Apply to Mentor
              </button>
            </div>
          </div>
        )}

        {/* Contact CTA */}
        <div className="bg-gradient-to-r from-blue-600 to-purple-600 rounded-lg p-8 text-center text-white">
          <h2 className="text-2xl font-bold mb-4">Stay Connected</h2>
          <p className="text-blue-100 mb-6 max-w-2xl mx-auto">
            Don't miss out on the latest community updates, events, and opportunities. 
            Join our growing community today!
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <a
              href="#"
              className="inline-flex items-center px-6 py-3 border border-transparent text-base font-medium rounded-md text-blue-600 bg-white hover:bg-gray-50 transition-colors"
            >
              <Github className="w-5 h-5 mr-2" />
              Follow on GitHub
            </a>
            <a
              href="#"
              className="inline-flex items-center px-6 py-3 border border-white text-base font-medium rounded-md text-white hover:bg-white hover:text-blue-600 transition-colors"
            >
              <Twitter className="w-5 h-5 mr-2" />
              Join Discord
            </a>
          </div>
        </div>
      </div>
    </div>
  );
}