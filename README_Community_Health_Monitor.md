# TiXL Community Health Monitoring System

A comprehensive community health monitoring system for the TiXL project that tracks GitHub metrics, Discord activity, NuGet download statistics, and community growth trends.

## Overview

This system provides automated collection, analysis, and reporting of community health metrics to help maintainers and community managers make data-driven decisions about project vitality and community engagement.

## Features

### 🚀 Automated Metrics Collection
- **GitHub Repository Metrics**: Stars, forks, issues, PRs, commits, contributors
- **Discord Community Activity**: Members, engagement, message volume, response times
- **NuGet Package Analytics**: Downloads, version adoption, growth rates
- **Quality Indicators**: Issue resolution rates, PR velocity, test coverage

### 📊 Comprehensive Reporting
- **Weekly Health Reports**: Automated generation with trend analysis
- **Real-time Dashboards**: Live monitoring of key metrics
- **Alert System**: Proactive notifications for critical issues
- **Predictive Analytics**: Trend forecasting and early warning detection

### 🔔 Alerting & Notifications
- **Multi-channel Alerts**: Slack, Discord, email notifications
- **Configurable Thresholds**: Customizable alert levels
- **Escalation Procedures**: Automated issue escalation
- **Smart Anomaly Detection**: AI-powered anomaly identification

## Quick Start

### Prerequisites

- Python 3.11 or higher
- GitHub Personal Access Token
- Discord Bot Token (optional)
- Slack/Discord webhook URLs (for alerts)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/TiXL-Project/TiXL.git
   cd TiXL
   ```

2. **Install Python dependencies**
   ```bash
   pip install -r requirements.txt
   ```

3. **Configure environment variables**
   ```bash
   # Required
   export GITHUB_TOKEN="your_github_personal_access_token"
   export GITHUB_OWNER="TiXL-Project"
   export GITHUB_REPO="TiXL"
   
   # Optional
   export DISCORD_BOT_TOKEN="your_discord_bot_token"
   export DISCORD_GUILD_ID="your_discord_guild_id"
   export SLACK_WEBHOOK_URL="your_slack_webhook_url"
   export DISCORD_ALERT_WEBHOOK="your_discord_webhook_url"
   ```

4. **Run the health monitor**
   ```bash
   # Single collection cycle
   python scripts/community-health-monitor.py --mode once
   
   # Scheduled collection (every hour)
   python scripts/community-health-monitor.py --mode scheduled --interval 1
   ```

5. **Generate weekly report**
   ```bash
   python scripts/weekly-health-report.py --date 2024-01-01
   ```

## Configuration

### Main Configuration File

Edit `config/health_monitor.yaml` to customize:
- Repository settings
- Alert thresholds
- Notification channels
- Data retention policies
- Performance settings

### Environment Variables

Required:
- `GITHUB_TOKEN`: GitHub Personal Access Token
- `GITHUB_OWNER`: Repository owner/organization
- `GITHUB_REPO`: Repository name

Optional:
- `DISCORD_BOT_TOKEN`: Discord bot token
- `DISCORD_GUILD_ID`: Discord server ID
- `SLACK_WEBHOOK_URL`: Slack webhook for alerts
- `DISCORD_ALERT_WEBHOOK`: Discord webhook for alerts
- `ALERT_EMAIL_RECIPIENTS`: Comma-separated email list

## Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│                    TiXL Community Health Monitor            │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────┐ │
│  │ Data Collection │  │  Analysis &     │  │ Reporting   │ │
│  │ - GitHub API    │  │  Alerts         │  │ - Weekly    │ │
│  │ - Discord API   │  │ - Health Calc   │  │ - Dashboard │ │
│  │ - NuGet API     │  │ - Anomaly Detect│  │ - Notifications│ │
│  └─────────────────┘  └─────────────────┘  └─────────────┘ │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────┐ │
│  │   Database      │  │   Storage       │  │  Automation │ │
│  │ - SQLite        │  │ - Local files   │  │ - GitHub    │ │
│  │ - Time series   │  │ - Artifacts     │  │ - Scheduled │ │
│  │ - Historical    │  │ - Backups       │  │ - CI/CD     │ │
│  └─────────────────┘  └─────────────────┘  └─────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Collection**: Automated API calls to GitHub, Discord, NuGet
2. **Processing**: Data cleaning, validation, and enrichment
3. **Analysis**: Health score calculation, trend analysis, anomaly detection
4. **Storage**: Historical data persistence in SQLite database
5. **Reporting**: Weekly report generation and visualization
6. **Alerting**: Multi-channel notifications for critical issues

## Usage Examples

### Command Line Usage

```bash
# Run single collection
python scripts/community-health-monitor.py --mode once

# Run scheduled collection
python scripts/community-health-monitor.py --mode scheduled --interval 1

# Generate weekly report
python scripts/weekly-health-report.py --date 2024-01-15

# Custom configuration
python scripts/community-health-monitor.py --config custom_config.yaml
```

### Python API Usage

```python
from community_health_monitor import CommunityHealthMonitor

# Initialize monitor
monitor = CommunityHealthMonitor()

# Run collection cycle
import asyncio
async def main():
    await monitor.run_collection_cycle()

asyncio.run(main())

# Generate report
from weekly_health_report import WeeklyHealthReporter
reporter = WeeklyHealthReporter()
report_path = reporter.generate_weekly_report()
```

## Dashboard Integration

### Web Dashboard Setup

1. **Install frontend dependencies**
   ```bash
   cd dashboard
   npm install
   ```

2. **Configure API endpoints**
   ```javascript
   // dashboard/src/config.js
   export const API_BASE_URL = 'https://api.tixl.community';
   export const WEBSOCKET_URL = 'wss://ws.tixl.community';
   ```

3. **Build and deploy**
   ```bash
   npm run build
   # Deploy to your hosting platform
   ```

### Dashboard Features

- **Executive View**: High-level KPIs and health scores
- **Operational View**: Real-time monitoring and alerts
- **Analytics View**: Trend analysis and predictive insights
- **Mobile Responsive**: Touch-friendly mobile interface

## GitHub Actions Integration

The system includes automated GitHub Actions workflows for:

### Daily Metrics Collection
- **Schedule**: Every day at 08:00 UTC
- **Triggers**: Automatic on schedule, manual on demand
- **Outputs**: Database updates, artifacts, notifications

### Weekly Report Generation
- **Schedule**: Every Monday at 09:00 UTC
- **Triggers**: Automatic after metrics collection
- **Outputs**: Markdown reports, GitHub issues, notifications

### Workflow Configuration

```yaml
# .github/workflows/community-health.yml
name: TiXL Community Health Monitoring

on:
  schedule:
    - cron: '0 8 * * *'  # Daily at 08:00 UTC
  workflow_dispatch:     # Manual trigger
```

## Alert Thresholds

### Critical Alerts (🚨 Immediate Response)
- GitHub stars declining >10% in 30 days
- Zero commits for >14 days
- Discord DAU dropping >50% in 7 days
- Issue resolution rate <80%
- CI/CD pipeline failing >24 hours

### Warning Alerts (⚠️ Monitor Closely)
- GitHub star growth <5% monthly
- New contributor rate <2 per month
- PR review time >5 days average
- Test coverage <80%

### Configuration

Edit `config/health_monitor.yaml` to customize thresholds:

```yaml
thresholds:
  critical:
    stars_decline_rate: -0.10
    commit_frequency: 5
    daily_active_users: 20
    issue_resolution_rate: 0.80
  
  warning:
    stars_growth_rate: 0.05
    new_contributor_rate: 2
    pr_review_time_days: 5
```

## Data Sources

### GitHub API
- **Rate Limit**: 5000 requests/hour for authenticated users
- **Endpoints**: Repository stats, issues, PRs, contributors
- **Authentication**: Personal Access Token or GitHub App

### Discord API
- **Rate Limit**: 50 requests/second globally
- **Endpoints**: Guild info, channels, messages, members
- **Authentication**: Bot token

### NuGet API
- **Rate Limit**: No strict limit documented
- **Endpoints**: Package metadata, download statistics
- **Authentication**: Not required for public packages

## Metrics Reference

### GitHub Metrics
- **Stars**: Repository popularity indicator
- **Forks**: Community engagement level
- **Issues**: Support and bug tracking activity
- **Pull Requests**: Development velocity
- **Commits**: Code activity frequency
- **Contributors**: Community diversity

### Discord Metrics
- **Total Members**: Community size
- **Daily Active Users**: Engagement level
- **Message Volume**: Discussion activity
- **Response Times**: Support quality
- **Growth Rate**: Community expansion

### NuGet Metrics
- **Total Downloads**: Usage popularity
- **Version Adoption**: Update velocity
- **Growth Rate**: Trend direction
- **Dependencies**: Ecosystem integration

## Troubleshooting

### Common Issues

1. **GitHub API Rate Limit**
   ```bash
   # Check current rate limit
   curl -H "Authorization: token $GITHUB_TOKEN" \
        https://api.github.com/rate_limit
   ```

2. **Database Permission Errors**
   ```bash
   # Ensure data directory exists and is writable
   mkdir -p data
   chmod 755 data
   ```

3. **Discord API Errors**
   ```bash
   # Verify bot permissions and guild access
   # Check bot token validity
   ```

4. **Missing Dependencies**
   ```bash
   # Install all required packages
   pip install -r requirements.txt
   ```

### Debug Mode

Enable debug logging:

```bash
# Set environment variable
export LOG_LEVEL=DEBUG

# Run with verbose output
python scripts/community-health-monitor.py --mode once --verbose
```

### Log Analysis

```bash
# View recent logs
tail -f logs/community_health.log

# Search for errors
grep ERROR logs/community_health.log

# Analyze collection performance
grep "Collection cycle" logs/community_health.log | tail -20
```

## Contributing

### Development Setup

1. **Fork the repository**
2. **Create feature branch**
3. **Install development dependencies**
   ```bash
   pip install -r requirements.txt
   pip install pytest black flake8 mypy
   ```
4. **Run tests**
   ```bash
   pytest tests/
   ```
5. **Code formatting**
   ```bash
   black scripts/
   flake8 scripts/
   ```

### Adding New Metrics

1. **Extend data collection**
   ```python
   # scripts/community-health-monitor.py
   def collect_new_metric(self):
       # Implement metric collection logic
       return metric_data
   ```

2. **Update analysis**
   ```python
   # Add to health score calculation
   # Update alert thresholds
   ```

3. **Add to reports**
   ```python
   # Update weekly report generator
   # Add to dashboard visualization
   ```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

- **Documentation**: [Project Wiki](../../wiki)
- **Issues**: [GitHub Issues](../../issues)
- **Discussions**: [GitHub Discussions](../../discussions)
- **Discord**: [TiXL Community Server](https://discord.gg/tixl)

## Changelog

### Version 1.0.0 (2024-01-XX)
- Initial release of community health monitoring system
- GitHub, Discord, and NuGet metrics collection
- Automated weekly reporting
- Multi-channel alerting system
- Web dashboard integration
- GitHub Actions automation

---

*Built with ❤️ for the TiXL community*