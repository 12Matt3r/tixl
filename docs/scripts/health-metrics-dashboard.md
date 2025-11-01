# Health Metrics Dashboard Guide

## Overview

This guide provides comprehensive instructions for creating visual dashboards from TiXL community health metrics. The dashboards enable real-time monitoring of community vitality, trend analysis, and proactive identification of community health issues.

## Table of Contents

1. [Dashboard Architecture](#dashboard-architecture)
2. [Data Sources Integration](#data-sources-integration)
3. [Dashboard Types](#dashboard-types)
4. [Implementation Guide](#implementation-guide)
5. [Visualization Components](#visualization-components)
6. [Alert System](#alert-system)
7. [Mobile Responsiveness](#mobile-responsiveness)
8. [Deployment Strategies](#deployment-strategies)

## Dashboard Architecture

### System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    TiXL Community Health Dashboard               │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │ Executive View  │  │ Operational View│  │ Analytics View  │ │
│  │ - Key KPIs      │  │ - Real-time     │  │ - Trends        │ │
│  │ - Health Score  │  │ - Alerts        │  │ - Predictions   │ │
│  │ - Quick Actions │  │ - Status        │  │ - Deep Analysis │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                     Data Layer                                  │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐            │
│  │ GitHub API   │ │ Discord API  │ │ NuGet API    │            │
│  │ - Metrics    │ │ - Activity   │ │ - Downloads  │            │
│  │ - Issues     │ │ - Members    │ │ - Versions   │            │
│  │ - PRs        │ │ - Messages   │ │ - Growth     │            │
│  └──────────────┘ └──────────────┘ └──────────────┘            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Technology Stack

#### Frontend Technologies
- **React/Vue.js**: Modern reactive dashboard framework
- **Chart.js/D3.js**: Data visualization libraries
- **Material-UI/Bootstrap**: UI component libraries
- **WebSocket**: Real-time data updates
- **PWA**: Progressive Web App capabilities

#### Backend Services
- **Node.js/Python**: API server implementation
- **WebSocket Server**: Real-time communication
- **Database**: Time-series data storage (InfluxDB/TimescaleDB)
- **Caching**: Redis for performance optimization

#### Cloud Infrastructure
- **AWS/Azure/GCP**: Cloud hosting platforms
- **CDN**: Global content delivery
- **Monitoring**: Application performance monitoring
- **CI/CD**: Automated deployment pipeline

## Data Sources Integration

### GitHub API Integration

```javascript
// GitHub API client configuration
const githubClient = {
  baseURL: 'https://api.github.com',
  endpoints: {
    repository: '/repos/{owner}/{repo}',
    issues: '/repos/{owner}/{repo}/issues',
    pullRequests: '/repos/{owner}/{repo}/pulls',
    contributors: '/repos/{owner}/{repo}/contributors',
    stats: '/repos/{owner}/{repo}/stats'
  },
  authentication: {
    type: 'token',
    header: 'Authorization',
    format: 'token {access_token}'
  }
};

// Real-time GitHub metrics collection
class GitHubMetricsCollector {
  async collectMetrics() {
    const [repo, issues, prs, contributors, activity] = await Promise.all([
      this.getRepositoryStats(),
      this.getIssueMetrics(),
      this.getPRMetrics(),
      this.getContributorStats(),
      this.getActivityStats()
    ]);
    
    return this.processMetrics({
      repository: repo,
      issues,
      pullRequests: prs,
      contributors,
      activity
    });
  }
  
  processMetrics(data) {
    return {
      stars: data.repository.stargazers_count,
      forks: data.repository.forks_count,
      watchers: data.repository.watchers_count,
      openIssues: data.issues.filter(i => i.state === 'open').length,
      closedIssues: data.issues.filter(i => i.state === 'closed').length,
      openPRs: data.pullRequests.filter(pr => pr.state === 'open').length,
      mergedPRs: data.pullRequests.filter(pr => pr.merged_at).length,
      contributorsCount: data.contributors.length,
      weeklyCommits: this.calculateWeeklyCommits(data.activity),
      lastCommitDate: data.repository.pushed_at
    };
  }
}
```

### Discord API Integration

```javascript
// Discord API client for community metrics
class DiscordMetricsCollector {
  constructor(botToken, guildId) {
    this.botToken = botToken;
    this.guildId = guildId;
    this.baseURL = 'https://discord.com/api/v10';
  }
  
  async collectCommunityMetrics() {
    const [guildInfo, memberCount, channelActivity] = await Promise.all([
      this.getGuildInfo(),
      this.getMemberCount(),
      this.analyzeChannelActivity()
    ]);
    
    return this.processCommunityData({
      guild: guildInfo,
      members: memberCount,
      activity: channelActivity
    });
  }
  
  async analyzeChannelActivity() {
    const channels = await this.getChannels();
    const activityData = {};
    
    for (const channel of channels) {
      if (channel.type === 0) { // Text channel
        activityData[channel.id] = await this.getChannelMessageStats(channel.id);
      }
    }
    
    return activityData;
  }
  
  processCommunityData(data) {
    return {
      totalMembers: data.guild.approximate_member_count,
      onlineMembers: data.guild.approximate_presence_count,
      dailyActiveUsers: this.calculateDAU(data.activity),
      weeklyActiveUsers: this.calculateWAU(data.activity),
      messageVolume: this.calculateMessageVolume(data.activity),
      responseTimes: this.calculateResponseTimes(data.activity),
      engagementRate: this.calculateEngagementRate(data)
    };
  }
}
```

### NuGet API Integration

```javascript
// NuGet API client for package metrics
class NuGetMetricsCollector {
  constructor(packages) {
    this.packages = packages;
    this.baseURL = 'https://api.nuget.org/v3-flatcontainer';
  }
  
  async collectPackageMetrics() {
    const packageMetrics = {};
    
    for (const packageId of this.packages) {
      packageMetrics[packageId] = await this.getPackageMetrics(packageId);
    }
    
    return packageMetrics;
  }
  
  async getPackageMetrics(packageId) {
    const [registration, statistics, versions] = await Promise.all([
      this.getPackageRegistration(packageId),
      this.getDownloadStatistics(packageId),
      this.getVersionAdoption(packageId)
    ]);
    
    return {
      totalDownloads: statistics.total || 0,
      weeklyDownloads: statistics.weekly || 0,
      monthlyDownloads: statistics.monthly || 0,
      growthRate: this.calculateGrowthRate(statistics),
      versionAdoption: versions,
      latestVersion: registration.version,
      dependencies: registration.dependencies
    };
  }
}
```

## Dashboard Types

### 1. Executive Dashboard

**Purpose**: High-level overview for stakeholders and decision-makers

#### Key Features
- **Health Score Card**: Overall community health with color-coded status
- **KPI Summary**: Top 5-7 metrics with trend indicators
- **Alert Summary**: Critical issues requiring immediate attention
- **Quick Actions**: One-click access to common tasks

#### Layout Structure
```
┌─────────────────────────────────────────────────────────────┐
│  TiXL Community Health - Executive Dashboard               │
├─────────────────────────────────────────────────────────────┤
│  🟢 Overall Health: 85/100          📊 Last Updated: Now    │
├─────────────────────────────────────────────────────────────┤
│  ⭐ GitHub: 1,234 (+5.2%)    💬 Discord: 456 DAU (+12%)    │
│  📦 Downloads: 89K (+15%)     🐛 Issues: 85% resolved      │
├─────────────────────────────────────────────────────────────┤
│  🚨 Active Alerts: 2        📈 Trend: Improving            │
│  [View Details] [Configure Alerts] [Generate Report]       │
└─────────────────────────────────────────────────────────────┘
```

#### Implementation Code
```javascript
// Executive Dashboard Component
import React from 'react';
import { Card, Row, Col, Progress, Badge, Alert } from 'antd';
import { GithubOutlined, MessageOutlined, DownloadOutlined } from '@ant-design/icons';

const ExecutiveDashboard = ({ metrics, alerts }) => {
  const healthScore = metrics.overallHealth;
  const getHealthColor = (score) => {
    if (score >= 80) return 'success';
    if (score >= 60) return 'warning';
    return 'danger';
  };

  return (
    <div className="executive-dashboard">
      {/* Health Score Header */}
      <Card className="health-score-header">
        <Row justify="space-between" align="middle">
          <Col>
            <h1>TiXL Community Health</h1>
            <Badge status={getHealthColor(healthScore)} text={`Health Score: ${healthScore}/100`} />
          </Col>
          <Col>
            <Progress
              type="circle"
              percent={healthScore}
              status={getHealthColor(healthScore)}
              size={80}
            />
          </Col>
        </Row>
      </Card>

      {/* KPI Summary Grid */}
      <Row gutter={[16, 16]} className="kpi-grid">
        <Col xs={12} sm={6}>
          <Card className="kpi-card">
            <GithubOutlined className="kpi-icon" />
            <div className="kpi-content">
              <div className="kpi-value">{metrics.github.stars.toLocaleString()}</div>
              <div className="kpi-label">GitHub Stars</div>
              <div className={`kpi-change ${metrics.github.starsGrowth >= 0 ? 'positive' : 'negative'}`}>
                {metrics.github.starsGrowth >= 0 ? '↗' : '↘'} {Math.abs(metrics.github.starsGrowth).toFixed(1)}%
              </div>
            </div>
          </Card>
        </Col>
        
        <Col xs={12} sm={6}>
          <Card className="kpi-card">
            <MessageOutlined className="kpi-icon" />
            <div className="kpi-content">
              <div className="kpi-value">{metrics.discord.dailyActiveUsers.toLocaleString()}</div>
              <div className="kpi-label">Daily Active Users</div>
              <div className={`kpi-change ${metrics.discord.dauGrowth >= 0 ? 'positive' : 'negative'}`}>
                {metrics.discord.dauGrowth >= 0 ? '↗' : '↘'} {Math.abs(metrics.discord.dauGrowth).toFixed(1)}%
              </div>
            </div>
          </Card>
        </Col>
        
        <Col xs={12} sm={6}>
          <Card className="kpi-card">
            <DownloadOutlined className="kpi-icon" />
            <div className="kpi-content">
              <div className="kpi-value">{(metrics.nuget.downloads / 1000).toFixed(0)}K</div>
              <div className="kpi-label">Weekly Downloads</div>
              <div className={`kpi-change ${metrics.nuget.downloadGrowth >= 0 ? 'positive' : 'negative'}`}>
                {metrics.nuget.downloadGrowth >= 0 ? '↗' : '↘'} {Math.abs(metrics.nuget.downloadGrowth).toFixed(1)}%
              </div>
            </div>
          </Card>
        </Col>
        
        <Col xs={12} sm={6}>
          <Card className="kpi-card">
            <div className="kpi-content">
              <div className="kpi-value">{metrics.github.issueResolutionRate.toFixed(0)}%</div>
              <div className="kpi-label">Issue Resolution</div>
              <div className="kpi-target">Target: 80%</div>
            </div>
          </Card>
        </Col>
      </Row>

      {/* Alert Summary */}
      {alerts.length > 0 && (
        <Card title="🚨 Active Alerts" className="alerts-card">
          {alerts.slice(0, 3).map((alert, index) => (
            <Alert
              key={index}
              message={alert.message}
              type={alert.severity}
              showIcon
              style={{ marginBottom: 8 }}
            />
          ))}
          {alerts.length > 3 && (
            <div className="more-alerts">+{alerts.length - 3} more alerts</div>
          )}
        </Card>
      )}
    </div>
  );
};

export default ExecutiveDashboard;
```

### 2. Operational Dashboard

**Purpose**: Real-time monitoring for community managers and maintainers

#### Key Features
- **Live Metrics**: Real-time updating counters and gauges
- **Activity Feed**: Recent events and transactions
- **Issue Tracker**: Open issues with resolution status
- **Alert Management**: Configurable alert thresholds

#### Layout Structure
```
┌─────────────────────────────────────────────────────────────┐
│  TiXL Operations Dashboard - Live Monitoring               │
├─────────────────────────────────────────────────────────────┤
│  🔴 GitHub API Rate: 4,850/5,000     🔄 Auto-refresh: 5min │
├─────────────────────────────────────────────────────────────┤
│  ┌─ Recent Activity ─┐  ┌─ Current Status ─┐  ┌─ Issues ─┐ │
│  │ • PR #123 merged  │  │ CI: ✅ Passing   │  │ 🟢 12 OK │ │
│  │ • Issue #456 closed│  │ Deploy: ✅ Ready│  │ 🟡 3 Warn│ │
│  │ • New contributor │  │ API: ✅ Healthy │  │ 🔴 1 Crit│ │
│  │ • Release v2.1.0  │  │ DB: ✅ Responsive│  └─────────┘ │
│  └───────────────────┘  └─────────────────┘               │
├─────────────────────────────────────────────────────────────┤
│  📊 Live Metrics (Updated every 30 seconds)                │
│  Commits: ████████░░ 85%    Issues: ████░░░░░ 45%          │
│  PRs: ██████████░░ 92%     Discord: ████████░░ 78%         │
└─────────────────────────────────────────────────────────────┘
```

#### Implementation Code
```javascript
// Operational Dashboard with Real-time Updates
import React, { useState, useEffect } from 'react';
import { Card, Row, Col, Timeline, Statistic, Progress, Alert, Tag } from 'antd';
import { SyncOutlined, CheckCircleOutlined, ExclamationCircleOutlined } from '@ant-design/icons';

const OperationalDashboard = () => {
  const [metrics, setMetrics] = useState({});
  const [alerts, setAlerts] = useState([]);
  const [activity, setActivity] = useState([]);
  const [isLoading, setIsLoading] = useState(false);

  // WebSocket connection for real-time updates
  useEffect(() => {
    const ws = new WebSocket('ws://localhost:8080/metrics');
    
    ws.onmessage = (event) => {
      const data = JSON.parse(event.data);
      setMetrics(data.metrics);
      setAlerts(data.alerts);
      setActivity(data.activity);
    };
    
    return () => ws.close();
  }, []);

  // Manual refresh function
  const refreshData = async () => {
    setIsLoading(true);
    try {
      const response = await fetch('/api/metrics/current');
      const data = await response.json();
      setMetrics(data);
    } catch (error) {
      console.error('Failed to refresh data:', error);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="operational-dashboard">
      {/* Header with Controls */}
      <Card>
        <Row justify="space-between" align="middle">
          <Col>
            <h2>TiXL Operations Dashboard</h2>
            <p>Real-time community monitoring and management</p>
          </Col>
          <Col>
            <SyncOutlined 
              spin={isLoading} 
              onClick={refreshData}
              style={{ fontSize: '24px', cursor: 'pointer' }}
            />
          </Col>
        </Row>
      </Card>

      {/* System Status Overview */}
      <Row gutter={[16, 16]} className="status-grid">
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="GitHub API Rate"
              value={metrics.githubRate || 4850}
              suffix="/ 5000"
              prefix={<CheckCircleOutlined style={{ color: '#52c41a' }} />}
            />
            <Progress 
              percent={((metrics.githubRate || 4850) / 5000) * 100} 
              showInfo={false}
              strokeColor="#52c41a"
            />
          </Card>
        </Col>
        
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="CI/CD Status"
              value="Passing"
              prefix={<CheckCircleOutlined style={{ color: '#52c41a' }} />}
            />
            <div>Last run: {metrics.lastCIRun || '2 minutes ago'}</div>
          </Card>
        </Col>
        
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Active Alerts"
              value={alerts.length}
              prefix={<ExclamationCircleOutlined style={{ color: alerts.length > 0 ? '#faad14' : '#52c41a' }} />}
            />
            <div>{alerts.filter(a => a.severity === 'critical').length} critical</div>
          </Card>
        </Col>
        
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Database Health"
              value="Responsive"
              prefix={<CheckCircleOutlined style={{ color: '#52c41a' }} />}
            />
            <div>Response: {metrics.dbResponseTime || '< 100ms'}</div>
          </Card>
        </Col>
      </Row>

      {/* Live Metrics */}
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          <Card title="📊 Live Metrics">
            <div className="metric-row">
              <div className="metric-label">Commits Today</div>
              <Progress percent={metrics.commitsPercent || 85} strokeColor="#1890ff" />
              <span className="metric-value">{metrics.commitsToday || 17}/20</span>
            </div>
            
            <div className="metric-row">
              <div className="metric-label">Issues Resolved</div>
              <Progress percent={metrics.issuesPercent || 45} strokeColor="#52c41a" />
              <span className="metric-value">{metrics.issuesResolved || 9}/20</span>
            </div>
            
            <div className="metric-row">
              <div className="metric-label">PRs Merged</div>
              <Progress percent={metrics.prsPercent || 92} strokeColor="#722ed1" />
              <span className="metric-value">{metrics.prsMerged || 11}/12</span>
            </div>
            
            <div className="metric-row">
              <div className="metric-label">Discord Engagement</div>
              <Progress percent={metrics.discordPercent || 78} strokeColor="#fa8c16" />
              <span className="metric-value">{metrics.discordEngagement || 78}%</span>
            </div>
          </Card>
        </Col>
        
        <Col xs={24} lg={12}>
          <Card title="🚨 Alert Summary">
            {alerts.length === 0 ? (
              <Alert message="No active alerts" type="success" showIcon />
            ) : (
              <Timeline>
                {alerts.slice(0, 5).map((alert, index) => (
                  <Timeline.Item
                    key={index}
                    color={alert.severity === 'critical' ? 'red' : 'orange'}
                  >
                    <div className="alert-item">
                      <div className="alert-message">{alert.message}</div>
                      <div className="alert-time">
                        {new Date(alert.timestamp).toLocaleTimeString()}
                      </div>
                      <Tag color={alert.severity === 'critical' ? 'red' : 'orange'}>
                        {alert.severity}
                      </Tag>
                    </div>
                  </Timeline.Item>
                ))}
              </Timeline>
            )}
          </Card>
        </Col>
      </Row>
    </div>
  );
};

export default OperationalDashboard;
```

### 3. Analytics Dashboard

**Purpose**: Deep-dive analysis and trend identification

#### Key Features
- **Trend Analysis**: Historical data visualization
- **Correlation Analysis**: Multi-metric relationships
- **Predictive Analytics**: Forecasting future trends
- **Custom Reports**: User-defined analysis

#### Implementation Code
```javascript
// Analytics Dashboard with Chart.js
import React, { useState, useEffect } from 'react';
import { Card, Row, Col, Select, DatePicker, Tabs } from 'antd';
import { Line, Bar, Doughnut, Scatter } from 'react-chartjs-2';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  BarElement,
  Title,
  Tooltip,
  Legend,
  ArcElement,
} from 'chart.js';

ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  BarElement,
  Title,
  Tooltip,
  Legend,
  ArcElement
);

const AnalyticsDashboard = () => {
  const [timeRange, setTimeRange] = useState('30d');
  const [selectedMetrics, setSelectedMetrics] = useState(['stars', 'forks', 'dau']);
  const [chartData, setChartData] = useState({});

  // Fetch historical data
  useEffect(() => {
    const fetchData = async () => {
      const response = await fetch(`/api/analytics/historical?range=${timeRange}&metrics=${selectedMetrics.join(',')}`);
      const data = await response.json();
      setChartData(data);
    };
    
    fetchData();
  }, [timeRange, selectedMetrics]);

  const trendChartOptions = {
    responsive: true,
    plugins: {
      legend: {
        position: 'top',
      },
      title: {
        display: true,
        text: 'Community Metrics Trend Analysis',
      },
    },
    scales: {
      y: {
        beginAtZero: true,
      },
    },
  };

  const trendChartData = {
    labels: chartData.dates || [],
    datasets: selectedMetrics.map((metric, index) => ({
      label: metric.toUpperCase(),
      data: chartData[metric] || [],
      borderColor: `hsl(${index * 120}, 70%, 50%)`,
      backgroundColor: `hsla(${index * 120}, 70%, 50%, 0.2)`,
    })),
  };

  const correlationData = {
    labels: ['High Engagement', 'Medium Engagement', 'Low Engagement'],
    datasets: [
      {
        data: [chartData.highEngagement || 35, chartData.mediumEngagement || 45, chartData.lowEngagement || 20],
        backgroundColor: [
          '#52c41a',
          '#faad14',
          '#f5222d',
        ],
      },
    ],
  };

  return (
    <div className="analytics-dashboard">
      <Card>
        <Row justify="space-between" align="middle">
          <Col>
            <h2>TiXL Analytics Dashboard</h2>
            <p>Deep analysis and trend identification</p>
          </Col>
          <Col>
            <Select
              value={timeRange}
              onChange={setTimeRange}
              style={{ width: 120, marginRight: 8 }}
            >
              <Select.Option value="7d">7 Days</Select.Option>
              <Select.Option value="30d">30 Days</Select.Option>
              <Select.Option value="90d">90 Days</Select.Option>
              <Select.Option value="1y">1 Year</Select.Option>
            </Select>
          </Col>
        </Row>
      </Card>

      <Tabs defaultActiveKey="trends">
        <Tabs.TabPane tab="📈 Trends" key="trends">
          <Row gutter={[16, 16]}>
            <Col xs={24} lg={16}>
              <Card title="Metric Trends Over Time">
                <Line data={trendChartData} options={trendChartOptions} />
              </Card>
            </Col>
            <Col xs={24} lg={8}>
              <Card title="Engagement Distribution">
                <Doughnut data={correlationData} />
              </Card>
            </Col>
          </Row>
        </Tabs.TabPane>
        
        <Tabs.TabPane tab="🔍 Correlations" key="correlations">
          <Row gutter={[16, 16]}>
            <Col span={24}>
              <Card title="Metric Correlation Analysis">
                <Scatter
                  data={{
                    datasets: [{
                      label: 'Stars vs DAU Correlation',
                      data: chartData.correlationData || [],
                      backgroundColor: 'rgba(54, 162, 235, 0.6)',
                    }]
                  }}
                  options={{
                    scales: {
                      x: {
                        title: {
                          display: true,
                          text: 'GitHub Stars'
                        }
                      },
                      y: {
                        title: {
                          display: true,
                          text: 'Daily Active Users'
                        }
                      }
                    }
                  }}
                />
              </Card>
            </Col>
          </Row>
        </Tabs.TabPane>
        
        <Tabs.TabPane tab="🎯 Predictions" key="predictions">
          <Row gutter={[16, 16]}>
            <Col span={24}>
              <Card title="📊 Predictive Analytics">
                <div className="prediction-grid">
                  <div className="prediction-item">
                    <h4>Next Month Stars</h4>
                    <div className="prediction-value">+{chartData.predictedStarsGrowth || 12.5}%</div>
                    <div className="prediction-confidence">85% confidence</div>
                  </div>
                  <div className="prediction-item">
                    <h4>Discord Growth</h4>
                    <div className="prediction-value">+{chartData.predictedDauGrowth || 8.3}%</div>
                    <div className="prediction-confidence">78% confidence</div>
                  </div>
                  <div className="prediction-item">
                    <h4>Download Trend</h4>
                    <div className="prediction-value">{chartData.predictedDownloadTrend || 'Stable'}</div>
                    <div className="prediction-confidence">72% confidence</div>
                  </div>
                </div>
              </Card>
            </Col>
          </Row>
        </Tabs.TabPane>
      </Tabs>
    </div>
  );
};

export default AnalyticsDashboard;
```

## Visualization Components

### 1. Health Score Gauge

```javascript
// Reusable Health Score Component
import React from 'react';
import { Gauge } from '@ant-design/plots';

const HealthScoreGauge = ({ score, size = 200 }) => {
  const config = {
    percent: score / 100,
    range: {
      color: score >= 80 ? '#52c41a' : score >= 60 ? '#faad14' : '#f5222d',
    },
    indicator: {
      pointer: {
        style: {
          stroke: '#D0D0D0',
        },
      },
      pin: {
        style: {
          stroke: '#D0D0D0',
        },
      },
    },
    statistic: {
      content: {
        style: {
          fontSize: '24px',
          lineHeight: '24px',
        },
        formatter: () => `${score}/100`,
      },
    },
  };

  return (
    <div className="health-score-gauge">
      <Gauge {...config} height={size} />
      <div className="gauge-label">
        {score >= 80 ? 'Excellent' : score >= 60 ? 'Good' : 'Needs Attention'}
      </div>
    </div>
  );
};

export default HealthScoreGauge;
```

### 2. Trend Indicators

```javascript
// Trend Arrow Component
import React from 'react';
import { ArrowUpOutlined, ArrowDownOutlined, MinusOutlined } from '@ant-design/icons';

const TrendIndicator = ({ value, threshold = 0, showPercentage = true }) => {
  let icon = <MinusOutlined />;
  let color = '#999';
  let direction = 'neutral';

  if (value > threshold) {
    icon = <ArrowUpOutlined />;
    color = '#52c41a';
    direction = 'up';
  } else if (value < threshold) {
    icon = <ArrowDownOutlined />;
    color = '#f5222d';
    direction = 'down';
  }

  return (
    <span className={`trend-indicator ${direction}`} style={{ color }}>
      {icon}
      {showPercentage && (
        <span className="trend-value">{Math.abs(value).toFixed(1)}%</span>
      )}
    </span>
  );
};

export default TrendIndicator;
```

### 3. Alert Cards

```javascript
// Alert Display Component
import React from 'react';
import { Alert, Card, Tag, Button } from 'antd';
import { CloseOutlined, EyeOutlined } from '@ant-design/icons';

const AlertCard = ({ alert, onDismiss, onView }) => {
  const getAlertIcon = (severity) => {
    switch (severity) {
      case 'critical':
        return '🚨';
      case 'warning':
        return '⚠️';
      default:
        return 'ℹ️';
    }
  };

  const getAlertColor = (severity) => {
    switch (severity) {
      case 'critical':
        return 'error';
      case 'warning':
        return 'warning';
      default:
        return 'info';
    }
  };

  return (
    <Card size="small" className={`alert-card severity-${alert.severity}`}>
      <div className="alert-header">
        <span className="alert-icon">{getAlertIcon(alert.severity)}</span>
        <span className="alert-type">{alert.type}</span>
        <Tag color={getAlertColor(alert.severity)}>{alert.severity}</Tag>
        <div className="alert-actions">
          <Button
            type="text"
            size="small"
            icon={<EyeOutlined />}
            onClick={() => onView(alert)}
          >
            View
          </Button>
          <Button
            type="text"
            size="small"
            icon={<CloseOutlined />}
            onClick={() => onDismiss(alert.id)}
          >
            Dismiss
          </Button>
        </div>
      </div>
      <div className="alert-message">{alert.message}</div>
      <div className="alert-timestamp">
        {new Date(alert.timestamp).toLocaleString()}
      </div>
    </Card>
  );
};

export default AlertCard;
```

## Alert System

### Alert Configuration

```javascript
// Alert Configuration Management
class AlertManager {
  constructor() {
    this.thresholds = {
      critical: {
        stars_decline_rate: -0.10,
        commit_frequency: 5,
        daily_active_users: 20,
        issue_resolution_rate: 0.80,
        pr_velocity_days: 7
      },
      warning: {
        stars_growth_rate: 0.05,
        new_contributor_rate: 2,
        release_frequency: 1
      }
    };
  }

  checkThresholds(metrics) {
    const alerts = [];

    // Check critical thresholds
    Object.entries(this.thresholds.critical).forEach(([metric, threshold]) => {
      if (metrics[metric] !== undefined) {
        if (this.shouldAlert(metrics[metric], threshold, 'critical', metric)) {
          alerts.push({
            id: `critical_${metric}_${Date.now()}`,
            type: 'metric',
            severity: 'critical',
            metric: metric,
            value: metrics[metric],
            threshold: threshold,
            message: this.generateAlertMessage(metric, metrics[metric], threshold, 'critical'),
            timestamp: new Date().toISOString(),
            autoResolve: false
          });
        }
      }
    });

    // Check warning thresholds
    Object.entries(this.thresholds.warning).forEach(([metric, threshold]) => {
      if (metrics[metric] !== undefined) {
        if (this.shouldAlert(metrics[metric], threshold, 'warning', metric)) {
          alerts.push({
            id: `warning_${metric}_${Date.now()}`,
            type: 'metric',
            severity: 'warning',
            metric: metric,
            value: metrics[metric],
            threshold: threshold,
            message: this.generateAlertMessage(metric, metrics[metric], threshold, 'warning'),
            timestamp: new Date().toISOString(),
            autoResolve: true
          });
        }
      }
    });

    return alerts;
  }

  shouldAlert(value, threshold, severity, metric) {
    // Define logic for when to trigger alerts based on metric type
    const declineMetrics = ['stars_decline_rate', 'downloads_decline_rate'];
    const frequencyMetrics = ['commit_frequency', 'release_frequency'];
    const rateMetrics = ['issue_resolution_rate', 'test_coverage'];
    const countMetrics = ['daily_active_users', 'new_contributor_rate'];

    if (declineMetrics.includes(metric)) {
      return severity === 'critical' ? value < threshold : value < threshold;
    }

    if (frequencyMetrics.includes(metric)) {
      return severity === 'critical' ? value < threshold : value < threshold;
    }

    if (rateMetrics.includes(metric)) {
      return severity === 'critical' ? value < threshold : value < threshold;
    }

    if (countMetrics.includes(metric)) {
      return severity === 'critical' ? value < threshold : value < threshold;
    }

    return false;
  }

  generateAlertMessage(metric, value, threshold, severity) {
    const messages = {
      stars_decline_rate: `GitHub stars declining at ${(value * 100).toFixed(1)}% (threshold: ${(threshold * 100).toFixed(1)}%)`,
      commit_frequency: `Only ${value} commits this week (threshold: ${threshold})`,
      daily_active_users: `Discord daily active users dropped to ${value} (threshold: ${threshold})`,
      issue_resolution_rate: `Issue resolution rate at ${(value * 100).toFixed(1)}% (threshold: ${(threshold * 100).toFixed(1)}%)`,
      pr_velocity_days: `Average PR review time: ${value} days (threshold: ${threshold})`,
      stars_growth_rate: `GitHub star growth rate ${(value * 100).toFixed(1)}% below target ${(threshold * 100).toFixed(1)}%`,
      new_contributor_rate: `Only ${value} new contributors this week (threshold: ${threshold})`,
      release_frequency: `No releases for ${value} weeks (threshold: ${threshold})`
    };

    return messages[metric] || `${metric}: ${value} (threshold: ${threshold})`;
  }
}
```

## Mobile Responsiveness

### Responsive Grid System

```css
/* Mobile-first responsive design */
.dashboard-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 16px;
  padding: 16px;
}

/* Tablet */
@media (min-width: 768px) {
  .dashboard-grid {
    grid-template-columns: repeat(2, 1fr);
    gap: 20px;
    padding: 20px;
  }
}

/* Desktop */
@media (min-width: 1024px) {
  .dashboard-grid {
    grid-template-columns: repeat(3, 1fr);
    gap: 24px;
    padding: 24px;
  }
}

/* Large desktop */
@media (min-width: 1440px) {
  .dashboard-grid {
    grid-template-columns: repeat(4, 1fr);
    gap: 24px;
    padding: 24px;
  }
}

/* KPI Cards responsive */
.kpi-card {
  min-height: 120px;
}

@media (max-width: 576px) {
  .kpi-card {
    min-height: 100px;
    padding: 12px;
  }
  
  .kpi-value {
    font-size: 1.5rem;
  }
  
  .kpi-label {
    font-size: 0.8rem;
  }
}

/* Chart responsive */
.chart-container {
  position: relative;
  width: 100%;
  height: 300px;
}

@media (max-width: 768px) {
  .chart-container {
    height: 250px;
  }
}

@media (max-width: 480px) {
  .chart-container {
    height: 200px;
  }
}
```

### Touch-Friendly Controls

```css
/* Touch-friendly button sizing */
.alert-actions button {
  min-height: 44px;
  min-width: 44px;
  padding: 8px 12px;
}

.dashboard-controls {
  display: flex;
  gap: 8px;
  margin-bottom: 16px;
}

@media (max-width: 768px) {
  .dashboard-controls {
    flex-direction: column;
    gap: 12px;
  }
  
  .dashboard-controls .ant-btn {
    width: 100%;
  }
}

/* Swipe gestures for mobile */
.dashboard-swipeable {
  touch-action: pan-y;
}

@media (hover: none) and (pointer: coarse) {
  /* Hide hover-only elements on touch devices */
  .hover-only {
    display: none;
  }
  
  /* Add touch feedback */
  .touchable {
    transition: transform 0.2s ease;
  }
  
  .touchable:active {
    transform: scale(0.95);
  }
}
```

## Deployment Strategies

### Docker Configuration

```dockerfile
# Multi-stage build for dashboard
FROM node:18-alpine AS builder

WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production

COPY . .
RUN npm run build

# Production stage
FROM nginx:alpine

# Copy built application
COPY --from=builder /app/dist /usr/share/nginx/html

# Copy nginx configuration
COPY nginx.conf /etc/nginx/nginx.conf

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

### Kubernetes Deployment

```yaml
# dashboard-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tixl-dashboard
  labels:
    app: tixl-dashboard
spec:
  replicas: 3
  selector:
    matchLabels:
      app: tixl-dashboard
  template:
    metadata:
      labels:
        app: tixl-dashboard
    spec:
      containers:
      - name: dashboard
        image: tixl/dashboard:latest
        ports:
        - containerPort: 80
        env:
        - name: API_BASE_URL
          value: "https://api.tixl.community"
        - name: WEBSOCKET_URL
          value: "wss://ws.tixl.community"
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5

---
apiVersion: v1
kind: Service
metadata:
  name: tixl-dashboard-service
spec:
  selector:
    app: tixl-dashboard
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
  type: LoadBalancer

---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: tixl-dashboard-ingress
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  tls:
  - hosts:
    - dashboard.tixl.community
    secretName: tixl-dashboard-tls
  rules:
  - host: dashboard.tixl.community
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: tixl-dashboard-service
            port:
              number: 80
```

### Monitoring and Observability

```javascript
// Monitoring integration
import { init as initAPM } from '@elastic/apm-rum';

const apm = initAPM({
  serviceName: 'tixl-dashboard',
  serverUrl: process.env.REACT_APP_APM_SERVER,
  environment: process.env.NODE_ENV,
});

// Performance monitoring
const trackPageLoad = (pageName) => {
  apm.startTransaction(`page-load-${pageName}`, 'page-load');
  
  window.addEventListener('load', () => {
    apm.endTransaction();
  });
};

// Error tracking
window.addEventListener('error', (event) => {
  apm.captureError(event.error);
});

// User interaction tracking
const trackUserAction = (action, category) => {
  apm.addLabel('user_action', action);
  apm.addLabel('action_category', category);
};

// API call monitoring
const monitorAPICall = async (url, options) => {
  const transaction = apm.startTransaction(`api-${url}`, 'api');
  const span = transaction.startSpan(url, 'external');
  
  try {
    const response = await fetch(url, options);
    span.end();
    transaction.setOutcome('success');
    return response;
  } catch (error) {
    span.end();
    transaction.setOutcome('failure');
    apm.captureError(error);
    throw error;
  } finally {
    transaction.end();
  }
};

export { trackPageLoad, trackUserAction, monitorAPICall };
```

### CI/CD Pipeline

```yaml
# .github/workflows/dashboard-deploy.yml
name: Deploy Dashboard

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup Node.js
      uses: actions/setup-node@v3
      with:
        node-version: '18'
        cache: 'npm'
    
    - name: Install dependencies
      run: npm ci
    
    - name: Run tests
      run: npm run test:ci
    
    - name: Run accessibility tests
      run: npm run test:a11y
    
    - name: Run Lighthouse CI
      run: npm run lhci

  build:
    needs: test
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup Node.js
      uses: actions/setup-node@v3
      with:
        node-version: '18'
        cache: 'npm'
    
    - name: Install dependencies
      run: npm ci
    
    - name: Build application
      run: npm run build
      env:
        REACT_APP_API_BASE_URL: ${{ secrets.API_BASE_URL }}
        REACT_APP_WEBSOCKET_URL: ${{ secrets.WEBSOCKET_URL }}
    
    - name: Upload build artifacts
      uses: actions/upload-artifact@v3
      with:
        name: build-files
        path: build/

  deploy:
    needs: build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
    - uses: actions/checkout@v3
    
    - name: Download build artifacts
      uses: actions/download-artifact@v3
      with:
        name: build-files
        path: build/
    
    - name: Deploy to production
      run: |
        # Deploy to your hosting platform
        # Examples: AWS S3, Google Cloud Storage, Netlify, Vercel, etc.
        echo "Deploying dashboard to production..."
    
    - name: Invalidate CDN cache
      run: |
        # Invalidate CDN cache for updated assets
        echo "Invalidating CDN cache..."
    
    - name: Notify deployment
      uses: 8398a7/action-slack@v3
      with:
        status: ${{ job.status }}
        text: "Dashboard deployed to production"
      env:
        SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK }}
      if: always()
```

This comprehensive guide provides the foundation for building robust, scalable, and user-friendly community health dashboards for the TiXL project. The implementation can be adapted based on specific requirements and technology preferences.