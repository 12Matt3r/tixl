#!/usr/bin/env python3
"""
TiXL Community Health Monitor
Automated data collection and analysis for community health metrics
"""

import os
import json
import time
import logging
import requests
import sqlite3
import asyncio
import aiohttp
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Tuple, Any
from dataclasses import dataclass, asdict
from concurrent.futures import ThreadPoolExecutor, as_completed
import statistics
from pathlib import Path
import yaml

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('community_health.log'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

@dataclass
class GitHubMetrics:
    """GitHub repository metrics data structure"""
    repository: str
    stars: int
    forks: int
    watchers: int
    open_issues: int
    closed_issues: int
    open_prs: int
    merged_prs: int
    commits_last_week: int
    contributors_count: int
    last_commit_date: str
    release_count: int
    created_at: str
    updated_at: str
    size: int
    language: str
    license: str
    topics: List[str]

@dataclass
class DiscordMetrics:
    """Discord community metrics data structure"""
    guild_id: str
    total_members: int
    online_members: int
    daily_active_users: int
    weekly_active_users: int
    monthly_active_users: int
    message_count_today: int
    message_count_week: int
    message_count_month: int
    new_members_today: int
    new_members_week: int
    new_members_month: int
    channel_activity: Dict[str, Dict[str, int]]
    response_times: List[float]

@dataclass
class NuGetMetrics:
    """NuGet package metrics data structure"""
    package_id: str
    total_downloads: int
    downloads_last_week: int
    downloads_last_month: int
    downloads_growth_rate: float
    version_adoption: Dict[str, int]
    latest_version: str
    last_updated: str
    dependencies: List[str]

@dataclass
class CommunityHealthReport:
    """Comprehensive community health report"""
    timestamp: str
    github_metrics: GitHubMetrics
    discord_metrics: DiscordMetrics
    nuget_metrics: NuGetMetrics
    calculated_scores: Dict[str, float]
    alerts: List[Dict[str, Any]]
    trends: Dict[str, str]

class ConfigManager:
    """Configuration management for the health monitor"""
    
    def __init__(self, config_path: str = "config/health_monitor.yaml"):
        self.config_path = config_path
        self.config = self._load_config()
    
    def _load_config(self) -> Dict[str, Any]:
        """Load configuration from YAML file"""
        default_config = {
            'github': {
                'token': os.getenv('GITHUB_TOKEN'),
                'owner': 'TiXL-Project',
                'repo': 'TiXL',
                'api_rate_limit': 5000,
                'rate_limit_buffer': 100
            },
            'discord': {
                'bot_token': os.getenv('DISCORD_BOT_TOKEN'),
                'guild_id': os.getenv('DISCORD_GUILD_ID'),
                'api_version': 'v10'
            },
            'nuget': {
                'packages': ['TiXL.Core', 'TiXL.Operators', 'TiXL.Graphics'],
                'api_base': 'https://api.nuget.org/v3'
            },
            'database': {
                'path': 'data/community_health.db',
                'backup_path': 'data/backups'
            },
            'thresholds': {
                'critical': {
                    'stars_decline_rate': -0.10,
                    'commit_frequency': 5,
                    'issue_resolution_rate': 0.80,
                    'pr_velocity_days': 7,
                    'daily_active_users': 20,
                    'response_time_hours': 2,
                    'download_decline_rate': -0.25,
                    'test_coverage': 0.70
                },
                'warning': {
                    'stars_growth_rate': 0.05,
                    'new_contributor_rate': 2,
                    'release_frequency': 1,
                    'documentation_completeness': 0.80
                }
            },
            'alerting': {
                'webhook_urls': {
                    'slack': os.getenv('SLACK_WEBHOOK_URL'),
                    'discord': os.getenv('DISCORD_ALERT_WEBHOOK')
                },
                'email_recipients': os.getenv('ALERT_EMAIL_RECIPIENTS', '').split(','),
                'notification_channels': ['slack', 'discord', 'email']
            }
        }
        
        try:
            if os.path.exists(self.config_path):
                with open(self.config_path, 'r') as f:
                    user_config = yaml.safe_load(f)
                    # Merge user config with defaults
                    default_config.update(user_config)
            else:
                # Create default config file
                os.makedirs(os.path.dirname(self.config_path), exist_ok=True)
                with open(self.config_path, 'w') as f:
                    yaml.dump(default_config, f, default_flow_style=False)
                logger.info(f"Created default config file: {self.config_path}")
        except Exception as e:
            logger.error(f"Error loading config: {e}. Using defaults.")
        
        return default_config

class DatabaseManager:
    """Database management for storing metrics data"""
    
    def __init__(self, db_path: str):
        self.db_path = db_path
        os.makedirs(os.path.dirname(db_path), exist_ok=True)
        self._init_database()
    
    def _init_database(self):
        """Initialize database tables"""
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.cursor()
            
            # GitHub metrics table
            cursor.execute('''
                CREATE TABLE IF NOT EXISTS github_metrics (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp TEXT NOT NULL,
                    repository TEXT NOT NULL,
                    stars INTEGER,
                    forks INTEGER,
                    watchers INTEGER,
                    open_issues INTEGER,
                    closed_issues INTEGER,
                    open_prs INTEGER,
                    merged_prs INTEGER,
                    commits_last_week INTEGER,
                    contributors_count INTEGER,
                    last_commit_date TEXT,
                    release_count INTEGER,
                    created_at TEXT,
                    updated_at TEXT,
                    size INTEGER,
                    language TEXT,
                    license TEXT,
                    topics TEXT,
                    UNIQUE(timestamp, repository)
                )
            ''')
            
            # Discord metrics table
            cursor.execute('''
                CREATE TABLE IF NOT EXISTS discord_metrics (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp TEXT NOT NULL,
                    guild_id TEXT NOT NULL,
                    total_members INTEGER,
                    online_members INTEGER,
                    daily_active_users INTEGER,
                    weekly_active_users INTEGER,
                    monthly_active_users INTEGER,
                    message_count_today INTEGER,
                    message_count_week INTEGER,
                    message_count_month INTEGER,
                    new_members_today INTEGER,
                    new_members_week INTEGER,
                    new_members_month INTEGER,
                    channel_activity TEXT,
                    response_times TEXT,
                    UNIQUE(timestamp, guild_id)
                )
            ''')
            
            # NuGet metrics table
            cursor.execute('''
                CREATE TABLE IF NOT EXISTS nuget_metrics (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp TEXT NOT NULL,
                    package_id TEXT NOT NULL,
                    total_downloads INTEGER,
                    downloads_last_week INTEGER,
                    downloads_last_month INTEGER,
                    downloads_growth_rate REAL,
                    version_adoption TEXT,
                    latest_version TEXT,
                    last_updated TEXT,
                    dependencies TEXT,
                    UNIQUE(timestamp, package_id)
                )
            ''')
            
            # Alerts table
            cursor.execute('''
                CREATE TABLE IF NOT EXISTS alerts (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp TEXT NOT NULL,
                    alert_type TEXT NOT NULL,
                    severity TEXT NOT NULL,
                    message TEXT NOT NULL,
                    metric_name TEXT,
                    metric_value REAL,
                    threshold_value REAL,
                    resolved BOOLEAN DEFAULT FALSE,
                    resolved_at TEXT
                )
            ''')
            
            conn.commit()
    
    def store_github_metrics(self, metrics: GitHubMetrics):
        """Store GitHub metrics in database"""
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.cursor()
            cursor.execute('''
                INSERT OR REPLACE INTO github_metrics (
                    timestamp, repository, stars, forks, watchers, open_issues,
                    closed_issues, open_prs, merged_prs, commits_last_week,
                    contributors_count, last_commit_date, release_count,
                    created_at, updated_at, size, language, license, topics
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ''', (
                datetime.utcnow().isoformat(), metrics.repository, metrics.stars,
                metrics.forks, metrics.watchers, metrics.open_issues, metrics.closed_issues,
                metrics.open_prs, metrics.merged_prs, metrics.commits_last_week,
                metrics.contributors_count, metrics.last_commit_date, metrics.release_count,
                metrics.created_at, metrics.updated_at, metrics.size, metrics.language,
                metrics.license, json.dumps(metrics.topics)
            ))
            conn.commit()
    
    def store_discord_metrics(self, metrics: DiscordMetrics):
        """Store Discord metrics in database"""
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.cursor()
            cursor.execute('''
                INSERT OR REPLACE INTO discord_metrics (
                    timestamp, guild_id, total_members, online_members,
                    daily_active_users, weekly_active_users, monthly_active_users,
                    message_count_today, message_count_week, message_count_month,
                    new_members_today, new_members_week, new_members_month,
                    channel_activity, response_times
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ''', (
                datetime.utcnow().isoformat(), metrics.guild_id, metrics.total_members,
                metrics.online_members, metrics.daily_active_users, metrics.weekly_active_users,
                metrics.monthly_active_users, metrics.message_count_today,
                metrics.message_count_week, metrics.message_count_month,
                metrics.new_members_today, metrics.new_members_week, metrics.new_members_month,
                json.dumps(metrics.channel_activity), json.dumps(metrics.response_times)
            ))
            conn.commit()
    
    def store_nuget_metrics(self, metrics: NuGetMetrics):
        """Store NuGet metrics in database"""
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.cursor()
            cursor.execute('''
                INSERT OR REPLACE INTO nuget_metrics (
                    timestamp, package_id, total_downloads, downloads_last_week,
                    downloads_last_month, downloads_growth_rate, version_adoption,
                    latest_version, last_updated, dependencies
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ''', (
                datetime.utcnow().isoformat(), metrics.package_id, metrics.total_downloads,
                metrics.downloads_last_week, metrics.downloads_last_month,
                metrics.downloads_growth_rate, json.dumps(metrics.version_adoption),
                metrics.latest_version, metrics.last_updated, json.dumps(metrics.dependencies)
            ))
            conn.commit()
    
    def store_alert(self, alert: Dict[str, Any]):
        """Store alert in database"""
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.cursor()
            cursor.execute('''
                INSERT INTO alerts (
                    timestamp, alert_type, severity, message, metric_name,
                    metric_value, threshold_value, resolved, resolved_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            ''', (
                datetime.utcnow().isoformat(), alert.get('type', 'unknown'),
                alert.get('severity', 'info'), alert.get('message', ''),
                alert.get('metric_name'), alert.get('metric_value'),
                alert.get('threshold_value'), False, None
            ))
            conn.commit()
    
    def get_recent_metrics(self, days: int = 30) -> Dict[str, List]:
        """Get recent metrics for trend analysis"""
        cutoff_date = (datetime.utcnow() - timedelta(days=days)).isoformat()
        
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.cursor()
            
            # Get recent GitHub metrics
            cursor.execute('''
                SELECT * FROM github_metrics WHERE timestamp >= ?
                ORDER BY timestamp DESC
            ''', (cutoff_date,))
            github_data = [dict(row) for row in cursor.fetchall()]
            
            # Get recent Discord metrics
            cursor.execute('''
                SELECT * FROM discord_metrics WHERE timestamp >= ?
                ORDER BY timestamp DESC
            ''', (cutoff_date,))
            discord_data = [dict(row) for row in cursor.fetchall()]
            
            # Get recent NuGet metrics
            cursor.execute('''
                SELECT * FROM nuget_metrics WHERE timestamp >= ?
                ORDER BY timestamp DESC
            ''', (cutoff_date,))
            nuget_data = [dict(row) for row in cursor.fetchall()]
        
        return {
            'github': github_data,
            'discord': discord_data,
            'nuget': nuget_data
        }

class GitHubAPIClient:
    """GitHub API client for collecting repository metrics"""
    
    def __init__(self, token: str, owner: str, repo: str):
        self.token = token
        self.owner = owner
        self.repo = repo
        self.base_url = "https://api.github.com"
        self.headers = {
            'Authorization': f'token {token}',
            'Accept': 'application/vnd.github.v3+json',
            'User-Agent': 'TiXL-Community-Health-Monitor'
        }
        self.rate_limit_remaining = 5000
    
    def check_rate_limit(self):
        """Check GitHub API rate limit"""
        try:
            response = requests.get(
                f"{self.base_url}/rate_limit",
                headers=self.headers,
                timeout=30
            )
            if response.status_code == 200:
                rate_data = response.json()
                self.rate_limit_remaining = rate_data['rate']['remaining']
                return self.rate_limit_remaining > 0
        except Exception as e:
            logger.error(f"Error checking rate limit: {e}")
        return False
    
    def collect_repository_metrics(self) -> Optional[GitHubMetrics]:
        """Collect comprehensive repository metrics"""
        if not self.check_rate_limit():
            logger.error("GitHub API rate limit exceeded")
            return None
        
        try:
            # Get basic repository info
            repo_response = requests.get(
                f"{self.base_url}/repos/{self.owner}/{self.repo}",
                headers=self.headers,
                timeout=30
            )
            repo_data = repo_response.json()
            
            # Get contributors count
            contributors_response = requests.get(
                f"{self.base_url}/repos/{self.owner}/{self.repo}/contributors",
                headers=self.headers,
                params={'per_page': 1},
                timeout=30
            )
            contributors_count = len(contributors_response.json()) if contributors_response.status_code == 200 else 0
            
            # Get commit activity (last 52 weeks)
            commits_response = requests.get(
                f"{self.base_url}/repos/{self.owner}/{self.repo}/stats/commit_activity",
                headers=self.headers,
                timeout=30
            )
            commits_data = commits_response.json() if commits_response.status_code == 200 else []
            commits_last_week = commits_data[-1]['total'] if commits_data else 0
            
            # Get issues
            issues_response = requests.get(
                f"{self.base_url}/repos/{self.owner}/{self.repo}/issues",
                headers=self.headers,
                params={'state': 'all', 'per_page': 1},
                timeout=30
            )
            issues_data = issues_response.json() if issues_response.status_code == 200 else []
            
            # Get pull requests
            prs_response = requests.get(
                f"{self.base_url}/repos/{self.owner}/{self.repo}/pulls",
                headers=self.headers,
                params={'state': 'all', 'per_page': 100},
                timeout=30
            )
            prs_data = prs_response.json() if prs_response.status_code == 200 else []
            
            # Calculate PR metrics
            open_prs = len([pr for pr in prs_data if pr['state'] == 'open'])
            merged_prs = len([pr for pr in prs_data if pr.get('merged_at')])
            
            # Get releases
            releases_response = requests.get(
                f"{self.base_url}/repos/{self.owner}/{self.repo}/releases",
                headers=self.headers,
                params={'per_page': 1},
                timeout=30
            )
            release_count = len(releases_response.json()) if releases_response.status_code == 200 else 0
            
            return GitHubMetrics(
                repository=f"{self.owner}/{self.repo}",
                stars=repo_data.get('stargazers_count', 0),
                forks=repo_data.get('forks_count', 0),
                watchers=repo_data.get('watchers_count', 0),
                open_issues=repo_data.get('open_issues_count', 0),
                closed_issues=len([issue for issue in issues_data if issue.get('state') == 'closed']) if issues_data else 0,
                open_prs=open_prs,
                merged_prs=merged_prs,
                commits_last_week=commits_last_week,
                contributors_count=contributors_count,
                last_commit_date=repo_data.get('pushed_at', ''),
                release_count=release_count,
                created_at=repo_data.get('created_at', ''),
                updated_at=repo_data.get('updated_at', ''),
                size=repo_data.get('size', 0),
                language=repo_data.get('language', ''),
                license=repo_data.get('license', {}).get('name', '') if repo_data.get('license') else '',
                topics=repo_data.get('topics', [])
            )
            
        except Exception as e:
            logger.error(f"Error collecting GitHub metrics: {e}")
            return None

class DiscordAPIClient:
    """Discord API client for collecting community metrics"""
    
    def __init__(self, bot_token: str, guild_id: str):
        self.bot_token = bot_token
        self.guild_id = guild_id
        self.base_url = "https://discord.com/api/v10"
        self.headers = {
            'Authorization': f'Bot {bot_token}',
            'Content-Type': 'application/json'
        }
    
    def collect_guild_metrics(self) -> Optional[DiscordMetrics]:
        """Collect guild member and activity metrics"""
        try:
            # Get guild information
            guild_response = requests.get(
                f"{self.base_url}/guilds/{self.guild_id}",
                headers=self.headers,
                timeout=30
            )
            if guild_response.status_code != 200:
                logger.error(f"Failed to get guild info: {guild_response.status_code}")
                return None
            
            guild_data = guild_response.json()
            
            # Get member count
            total_members = guild_data.get('approximate_member_count', 0)
            online_members = guild_data.get('approximate_presence_count', 0)
            
            # Calculate activity metrics (simplified for this example)
            # In a real implementation, you'd analyze message history
            daily_active_users = online_members  # Simplified calculation
            weekly_active_users = int(total_members * 0.3)  # Estimate
            monthly_active_users = int(total_members * 0.6)  # Estimate
            
            # Mock message counts (would be calculated from actual message data)
            message_count_today = 0  # Would analyze recent messages
            message_count_week = 0   # Would analyze week of messages
            message_count_month = 0  # Would analyze month of messages
            
            # Mock new member counts (would track from join events)
            new_members_today = 0
            new_members_week = 0
            new_members_month = 0
            
            # Mock channel activity (would analyze actual channels)
            channel_activity = {}
            
            # Mock response times (would calculate from message threads)
            response_times = []
            
            return DiscordMetrics(
                guild_id=self.guild_id,
                total_members=total_members,
                online_members=online_members,
                daily_active_users=daily_active_users,
                weekly_active_users=weekly_active_users,
                monthly_active_users=monthly_active_users,
                message_count_today=message_count_today,
                message_count_week=message_count_week,
                message_count_month=message_count_month,
                new_members_today=new_members_today,
                new_members_week=new_members_week,
                new_members_month=new_members_month,
                channel_activity=channel_activity,
                response_times=response_times
            )
            
        except Exception as e:
            logger.error(f"Error collecting Discord metrics: {e}")
            return None

class NuGetAPIClient:
    """NuGet API client for collecting package metrics"""
    
    def __init__(self, packages: List[str]):
        self.packages = packages
        self.base_url = "https://api.nuget.org/v3-flatcontainer"
    
    async def collect_package_metrics(self, package_id: str) -> Optional[NuGetMetrics]:
        """Collect metrics for a specific NuGet package"""
        try:
            async with aiohttp.ClientSession() as session:
                # Get package registration (metadata)
                registration_url = f"{self.base_url}/{package_id}/index.json"
                async with session.get(registration_url, timeout=30) as response:
                    if response.status != 200:
                        logger.error(f"Failed to get package {package_id}: {response.status}")
                        return None
                    
                    registration_data = await response.json()
                    
                # Get latest version and metadata
                versions = registration_data.get('versions', [])
                if not versions:
                    return None
                
                latest_version = max(versions)
                package_url = f"{self.base_url}/{package_id}/{latest_version}/{package_id}.nuspec"
                
                async with session.get(package_url, timeout=30) as response:
                    nuspec_data = await response.text()
                    
                # Parse nuspec (simplified - would use proper XML parsing)
                # For this example, we'll use mock data
                
                # Mock download statistics (would use NuGet statistics API)
                total_downloads = 0  # Would query statistics API
                downloads_last_week = 0
                downloads_last_month = 0
                downloads_growth_rate = 0.0
                
                # Mock version adoption (would analyze download statistics)
                version_adoption = {latest_version: total_downloads}
                
                # Mock dependencies (would parse from nuspec)
                dependencies = []
                
                # Mock last updated
                last_updated = datetime.utcnow().isoformat()
                
                return NuGetMetrics(
                    package_id=package_id,
                    total_downloads=total_downloads,
                    downloads_last_week=downloads_last_week,
                    downloads_last_month=downloads_last_month,
                    downloads_growth_rate=downloads_growth_rate,
                    version_adoption=version_adoption,
                    latest_version=latest_version,
                    last_updated=last_updated,
                    dependencies=dependencies
                )
                
        except Exception as e:
            logger.error(f"Error collecting NuGet metrics for {package_id}: {e}")
            return None
    
    async def collect_all_packages_metrics(self) -> List[NuGetMetrics]:
        """Collect metrics for all configured packages"""
        async with aiohttp.ClientSession() as session:
            tasks = [self.collect_package_metrics(package_id) for package_id in self.packages]
            results = await asyncio.gather(*tasks, return_exceptions=True)
            
            metrics = []
            for result in results:
                if isinstance(result, NuGetMetrics):
                    metrics.append(result)
                else:
                    logger.error(f"Error collecting metrics: {result}")
            
            return metrics

class HealthAnalyzer:
    """Analyze collected metrics and generate health scores"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.thresholds = config.get('thresholds', {})
    
    def calculate_github_health_score(self, current: GitHubMetrics, 
                                     historical: List[GitHubMetrics]) -> float:
        """Calculate GitHub repository health score"""
        score = 100.0
        
        # Star growth/decline penalty
        if historical:
            prev_stars = historical[0].stars if historical else current.stars
            if prev_stars > 0:
                growth_rate = (current.stars - prev_stars) / prev_stars
                if growth_rate < self.thresholds['critical']['stars_decline_rate']:
                    score -= 30
                elif growth_rate < self.thresholds['warning']['stars_growth_rate']:
                    score -= 10
        
        # Issue resolution rate
        total_issues = current.open_issues + current.closed_issues
        if total_issues > 0:
            resolution_rate = current.closed_issues / total_issues
            if resolution_rate < self.thresholds['critical']['issue_resolution_rate']:
                score -= 20
        
        # Commit frequency
        if current.commits_last_week < self.thresholds['critical']['commit_frequency']:
            score -= 25
        
        # PR velocity
        if current.open_prs > 0 and current.merged_prs == 0:
            score -= 15
        
        return max(0.0, score)
    
    def calculate_discord_health_score(self, current: DiscordMetrics,
                                      historical: List[DiscordMetrics]) -> float:
        """Calculate Discord community health score"""
        score = 100.0
        
        # Daily active users threshold
        if current.daily_active_users < self.thresholds['critical']['daily_active_users']:
            score -= 40
        
        # Member growth
        if historical:
            prev_members = historical[0].total_members if historical else current.total_members
            if current.total_members > prev_members:
                growth_rate = (current.total_members - prev_members) / prev_members
                if growth_rate < 0.01:  # 1% growth threshold
                    score -= 15
        
        # Online member percentage
        if current.total_members > 0:
            online_rate = current.online_members / current.total_members
            if online_rate < 0.1:  # Less than 10% online
                score -= 20
        
        return max(0.0, score)
    
    def calculate_nuget_health_score(self, current: NuGetMetrics,
                                    historical: List[NuGetMetrics]) -> float:
        """Calculate NuGet package health score"""
        score = 100.0
        
        # Download growth rate
        if current.downloads_growth_rate < self.thresholds['critical']['download_decline_rate']:
            score -= 35
        
        # Version adoption (assuming latest version should have most downloads)
        if current.version_adoption:
            total_downloads = sum(current.version_adoption.values())
            latest_version_downloads = current.version_adoption.get(current.latest_version, 0)
            if total_downloads > 0:
                adoption_rate = latest_version_downloads / total_downloads
                if adoption_rate < 0.5:  # Less than 50% adoption of latest version
                    score -= 20
        
        return max(0.0, score)
    
    def detect_anomalies(self, current_github: GitHubMetrics,
                        current_discord: DiscordMetrics,
                        current_nuget: List[NuGetMetrics],
                        historical: Dict[str, List]) -> List[Dict[str, Any]]:
        """Detect anomalies and generate alerts"""
        alerts = []
        
        # GitHub anomalies
        if current_github.commits_last_week == 0:
            alerts.append({
                'type': 'github',
                'severity': 'critical',
                'message': 'No commits in the last week',
                'metric_name': 'commits_last_week',
                'metric_value': 0,
                'threshold_value': 1
            })
        
        if current_github.open_issues > current_github.closed_issues * 2:
            alerts.append({
                'type': 'github',
                'severity': 'warning',
                'message': 'Issue backlog is growing faster than closure rate',
                'metric_name': 'issue_backlog_ratio',
                'metric_value': current_github.open_issues / max(1, current_github.closed_issues),
                'threshold_value': 2.0
            })
        
        # Discord anomalies
        if current_discord.daily_active_users < self.thresholds['critical']['daily_active_users']:
            alerts.append({
                'type': 'discord',
                'severity': 'critical',
                'message': 'Low daily active users',
                'metric_name': 'daily_active_users',
                'metric_value': current_discord.daily_active_users,
                'threshold_value': self.thresholds['critical']['daily_active_users']
            })
        
        # NuGet anomalies
        for package in current_nuget:
            if package.downloads_growth_rate < -0.5:  # 50% decline
                alerts.append({
                    'type': 'nuget',
                    'severity': 'critical',
                    'message': f'Significant download decline for {package.package_id}',
                    'metric_name': 'downloads_growth_rate',
                    'metric_value': package.downloads_growth_rate,
                    'threshold_value': -0.5
                })
        
        return alerts
    
    def analyze_trends(self, historical: Dict[str, List]) -> Dict[str, str]:
        """Analyze trends from historical data"""
        trends = {}
        
        # GitHub trends
        github_data = historical.get('github', [])
        if len(github_data) >= 2:
            recent_stars = github_data[0]['stars']
            older_stars = github_data[-1]['stars']
            
            if recent_stars > older_stars:
                growth_rate = ((recent_stars - older_stars) / older_stars) * 100
                trends['github_stars'] = f"Growing by {growth_rate:.1f}% over last 30 days"
            elif recent_stars < older_stars:
                decline_rate = ((older_stars - recent_stars) / older_stars) * 100
                trends['github_stars'] = f"Declining by {decline_rate:.1f}% over last 30 days"
            else:
                trends['github_stars'] = "Stable star count"
        
        # Discord trends
        discord_data = historical.get('discord', [])
        if len(discord_data) >= 2:
            recent_members = discord_data[0]['total_members']
            older_members = discord_data[-1]['total_members']
            
            if recent_members > older_members:
                growth_rate = ((recent_members - older_members) / older_members) * 100
                trends['discord_members'] = f"Growing by {growth_rate:.1f}% over last 30 days"
            else:
                trends['discord_members'] = "Stable or declining member count"
        
        return trends

class AlertManager:
    """Manage and send alerts"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.alerting_config = config.get('alerting', {})
    
    async def send_alert(self, alert: Dict[str, Any]):
        """Send alert through configured channels"""
        message = self._format_alert_message(alert)
        
        # Send to configured channels
        for channel in self.alerting_config.get('notification_channels', []):
            if channel == 'slack' and self.alerting_config.get('webhook_urls', {}).get('slack'):
                await self._send_slack_alert(message)
            elif channel == 'discord' and self.alerting_config.get('webhook_urls', {}).get('discord'):
                await self._send_discord_alert(message)
            elif channel == 'email':
                await self._send_email_alert(message, alert)
    
    def _format_alert_message(self, alert: Dict[str, Any]) -> str:
        """Format alert message for different channels"""
        severity_emoji = {
            'critical': '🚨',
            'warning': '⚠️',
            'info': 'ℹ️'
        }
        
        emoji = severity_emoji.get(alert.get('severity', 'info'), 'ℹ️')
        
        return (
            f"{emoji} TiXL Community Health Alert\\n"
            f"**Type:** {alert.get('type', 'Unknown')}\\n"
            f"**Severity:** {alert.get('severity', 'Unknown').upper()}\\n"
            f"**Message:** {alert.get('message', 'No message')}\\n"
            f"**Metric:** {alert.get('metric_name', 'N/A')}\\n"
            f"**Value:** {alert.get('metric_value', 'N/A')}\\n"
            f"**Threshold:** {alert.get('threshold_value', 'N/A')}\\n"
            f"**Time:** {datetime.utcnow().isoformat()}"
        )
    
    async def _send_slack_alert(self, message: str):
        """Send alert to Slack"""
        webhook_url = self.alerting_config.get('webhook_urls', {}).get('slack')
        if not webhook_url:
            return
        
        try:
            async with aiohttp.ClientSession() as session:
                payload = {'text': message}
                async with session.post(webhook_url, json=payload, timeout=30) as response:
                    if response.status == 200:
                        logger.info("Alert sent to Slack successfully")
                    else:
                        logger.error(f"Failed to send Slack alert: {response.status}")
        except Exception as e:
            logger.error(f"Error sending Slack alert: {e}")
    
    async def _send_discord_alert(self, message: str):
        """Send alert to Discord webhook"""
        webhook_url = self.alerting_config.get('webhook_urls', {}).get('discord')
        if not webhook_url:
            return
        
        try:
            async with aiohttp.ClientSession() as session:
                payload = {'content': message}
                async with session.post(webhook_url, json=payload, timeout=30) as response:
                    if response.status == 204:
                        logger.info("Alert sent to Discord successfully")
                    else:
                        logger.error(f"Failed to send Discord alert: {response.status}")
        except Exception as e:
            logger.error(f"Error sending Discord alert: {e}")
    
    async def _send_email_alert(self, message: str, alert: Dict[str, Any]):
        """Send alert via email (placeholder implementation)"""
        # This would integrate with an email service like SendGrid, AWS SES, etc.
        logger.info(f"Email alert would be sent: {alert}")

class CommunityHealthMonitor:
    """Main community health monitoring system"""
    
    def __init__(self, config_path: str = "config/health_monitor.yaml"):
        self.config_manager = ConfigManager(config_path)
        self.config = self.config_manager.config
        
        # Initialize components
        self.db_manager = DatabaseManager(self.config['database']['path'])
        self.github_client = GitHubAPIClient(
            self.config['github']['token'],
            self.config['github']['owner'],
            self.config['github']['repo']
        )
        self.discord_client = DiscordAPIClient(
            self.config['discord']['bot_token'],
            self.config['discord']['guild_id']
        )
        self.nuget_client = NuGetAPIClient(self.config['nuget']['packages'])
        self.health_analyzer = HealthAnalyzer(self.config)
        self.alert_manager = AlertManager(self.config)
    
    async def collect_all_metrics(self) -> Optional[CommunityHealthReport]:
        """Collect metrics from all sources"""
        logger.info("Starting metrics collection...")
        
        # Collect GitHub metrics
        github_metrics = self.github_client.collect_repository_metrics()
        if not github_metrics:
            logger.error("Failed to collect GitHub metrics")
            return None
        
        # Collect Discord metrics
        discord_metrics = self.discord_client.collect_guild_metrics()
        if not discord_metrics:
            logger.error("Failed to collect Discord metrics")
            return None
        
        # Collect NuGet metrics
        nuget_metrics = await self.nuget_client.collect_all_packages_metrics()
        
        # Get historical data for trend analysis
        historical = self.db_manager.get_recent_metrics()
        
        # Calculate health scores
        github_score = self.health_analyzer.calculate_github_health_score(
            github_metrics, historical['github']
        )
        discord_score = self.health_analyzer.calculate_discord_health_score(
            discord_metrics, historical['discord']
        )
        
        nuget_scores = []
        for package_metrics in nuget_metrics:
            score = self.health_analyzer.calculate_nuget_health_score(
                package_metrics, [m for m in historical['nuget'] if m['package_id'] == package_metrics.package_id]
            )
            nuget_scores.append(score)
        
        avg_nuget_score = statistics.mean(nuget_scores) if nuget_scores else 0
        
        # Calculate overall health score
        overall_score = (github_score + discord_score + avg_nuget_score) / 3
        
        # Detect anomalies and generate alerts
        alerts = self.health_analyzer.detect_anomalies(
            github_metrics, discord_metrics, nuget_metrics, historical
        )
        
        # Analyze trends
        trends = self.health_analyzer.analyze_trends(historical)
        
        # Create comprehensive report
        report = CommunityHealthReport(
            timestamp=datetime.utcnow().isoformat(),
            github_metrics=github_metrics,
            discord_metrics=discord_metrics,
            nuget_metrics=nuget_metrics[0] if nuget_metrics else None,  # Primary package
            calculated_scores={
                'github_health': github_score,
                'discord_health': discord_score,
                'nuget_health': avg_nuget_score,
                'overall_health': overall_score
            },
            alerts=alerts,
            trends=trends
        )
        
        return report
    
    async def store_and_alert(self, report: CommunityHealthReport):
        """Store metrics and send alerts"""
        # Store metrics in database
        self.db_manager.store_github_metrics(report.github_metrics)
        self.db_manager.store_discord_metrics(report.discord_metrics)
        if report.nuget_metrics:
            self.db_manager.store_nuget_metrics(report.nuget_metrics)
        
        # Send alerts
        for alert in report.alerts:
            self.db_manager.store_alert(alert)
            await self.alert_manager.send_alert(alert)
    
    async def run_collection_cycle(self):
        """Run a complete metrics collection cycle"""
        try:
            report = await self.collect_all_metrics()
            if report:
                await self.store_and_alert(report)
                logger.info(f"Collection cycle completed. Overall health score: {report.calculated_scores['overall_health']:.1f}")
                
                # Save report to file
                report_path = f"reports/health_report_{datetime.utcnow().strftime('%Y%m%d_%H%M%S')}.json"
                os.makedirs('reports', exist_ok=True)
                with open(report_path, 'w') as f:
                    json.dump(asdict(report), f, indent=2, default=str)
                logger.info(f"Report saved to {report_path}")
            else:
                logger.error("Failed to generate health report")
        except Exception as e:
            logger.error(f"Error in collection cycle: {e}")
    
    def run_scheduled_collection(self, interval_hours: int = 1):
        """Run scheduled collection with specified interval"""
        logger.info(f"Starting scheduled collection every {interval_hours} hours")
        
        while True:
            asyncio.run(self.run_collection_cycle())
            logger.info(f"Sleeping for {interval_hours} hours...")
            time.sleep(interval_hours * 3600)

async def main():
    """Main entry point"""
    monitor = CommunityHealthMonitor()
    
    # Run single collection cycle
    await monitor.run_collection_cycle()

if __name__ == "__main__":
    import argparse
    
    parser = argparse.ArgumentParser(description='TiXL Community Health Monitor')
    parser.add_argument('--mode', choices=['once', 'scheduled'], default='once',
                       help='Run mode: once or scheduled')
    parser.add_argument('--interval', type=int, default=1,
                       help='Collection interval in hours (for scheduled mode)')
    parser.add_argument('--config', default='config/health_monitor.yaml',
                       help='Configuration file path')
    
    args = parser.parse_args()
    
    monitor = CommunityHealthMonitor(args.config)
    
    if args.mode == 'once':
        asyncio.run(monitor.run_collection_cycle())
    elif args.mode == 'scheduled':
        monitor.run_scheduled_collection(args.interval)