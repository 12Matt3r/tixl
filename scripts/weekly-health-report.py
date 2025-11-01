#!/usr/bin/env python3
"""
TiXL Weekly Community Health Report Generator
Automated weekly reporting system for community health metrics
"""

import os
import json
import sqlite3
import asyncio
import logging
from datetime import datetime, timedelta
from typing import Dict, List, Any, Optional
from dataclasses import dataclass
import statistics
from pathlib import Path
import matplotlib.pyplot as plt
import matplotlib.dates as mdates
from matplotlib.patches import Rectangle
import pandas as pd
import seaborn as sns
from collections import defaultdict
import numpy as np
import base64
from io import BytesIO

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

@dataclass
class WeeklyReportData:
    """Weekly report data structure"""
    week_start: str
    week_end: str
    github_summary: Dict[str, Any]
    discord_summary: Dict[str, Any]
    nuget_summary: Dict[str, Any]
    trends: Dict[str, Any]
    alerts: List[Dict[str, Any]]
    recommendations: List[str]
    health_scores: Dict[str, float]

class DatabaseManager:
    """Database manager for report generation"""
    
    def __init__(self, db_path: str):
        self.db_path = db_path
    
    def get_weekly_data(self, week_start: datetime, week_end: datetime) -> Dict[str, List]:
        """Get metrics data for specified week"""
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.cursor()
            
            # GitHub data
            cursor.execute('''
                SELECT * FROM github_metrics 
                WHERE timestamp >= ? AND timestamp <= ?
                ORDER BY timestamp ASC
            ''', (week_start.isoformat(), week_end.isoformat()))
            github_data = [dict(row) for row in cursor.fetchall()]
            
            # Discord data
            cursor.execute('''
                SELECT * FROM discord_metrics 
                WHERE timestamp >= ? AND timestamp <= ?
                ORDER BY timestamp ASC
            ''', (week_start.isoformat(), week_end.isoformat()))
            discord_data = [dict(row) for row in cursor.fetchall()]
            
            # NuGet data
            cursor.execute('''
                SELECT * FROM nuget_metrics 
                WHERE timestamp >= ? AND timestamp <= ?
                ORDER BY timestamp ASC
            ''', (week_start.isoformat(), week_end.isoformat()))
            nuget_data = [dict(row) for row in cursor.fetchall()]
            
            # Alerts data
            cursor.execute('''
                SELECT * FROM alerts 
                WHERE timestamp >= ? AND timestamp <= ?
                ORDER BY timestamp ASC
            ''', (week_start.isoformat(), week_end.isoformat()))
            alerts_data = [dict(row) for row in cursor.fetchall()]
        
        return {
            'github': github_data,
            'discord': discord_data,
            'nuget': nuget_data,
            'alerts': alerts_data
        }
    
    def get_historical_data(self, days: int = 90) -> Dict[str, List]:
        """Get historical data for trend analysis"""
        cutoff_date = (datetime.utcnow() - timedelta(days=days)).isoformat()
        
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.cursor()
            
            # Historical GitHub data
            cursor.execute('''
                SELECT * FROM github_metrics 
                WHERE timestamp >= ?
                ORDER BY timestamp ASC
            ''', (cutoff_date,))
            github_data = [dict(row) for row in cursor.fetchall()]
            
            # Historical Discord data
            cursor.execute('''
                SELECT * FROM discord_metrics 
                WHERE timestamp >= ?
                ORDER BY timestamp ASC
            ''', (cutoff_date,))
            discord_data = [dict(row) for row in cursor.fetchall()]
            
            # Historical NuGet data
            cursor.execute('''
                SELECT * FROM nuget_metrics 
                WHERE timestamp >= ?
                ORDER BY timestamp ASC
            ''', (cutoff_date,))
            nuget_data = [dict(row) for row in cursor.fetchall()]
        
        return {
            'github': github_data,
            'discord': discord_data,
            'nuget': nuget_data
        }

class ReportGenerator:
    """Generate comprehensive weekly reports"""
    
    def __init__(self, db_manager: DatabaseManager):
        self.db_manager = db_manager
        self.output_dir = Path("reports/weekly")
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
        # Set up matplotlib style
        plt.style.use('seaborn-v0_8')
        self.color_palette = sns.color_palette("husl", 12)
    
    def calculate_growth_rates(self, current_week: List[Dict], 
                              previous_week: List[Dict], 
                              metric: str) -> Dict[str, float]:
        """Calculate week-over-week growth rates"""
        if not current_week or not previous_week:
            return {'current': 0, 'previous': 0, 'growth_rate': 0}
        
        current_values = [item.get(metric, 0) for item in current_week if metric in item]
        previous_values = [item.get(metric, 0) for item in previous_week if metric in item]
        
        if not current_values or not previous_values:
            return {'current': 0, 'previous': 0, 'growth_rate': 0}
        
        current_avg = statistics.mean(current_values)
        previous_avg = statistics.mean(previous_values)
        
        if previous_avg == 0:
            growth_rate = 100 if current_avg > 0 else 0
        else:
            growth_rate = ((current_avg - previous_avg) / previous_avg) * 100
        
        return {
            'current': current_avg,
            'previous': previous_avg,
            'growth_rate': growth_rate
        }
    
    def generate_github_summary(self, week_data: List[Dict], 
                               historical_data: List[Dict]) -> Dict[str, Any]:
        """Generate GitHub metrics summary"""
        if not week_data:
            return {'error': 'No GitHub data available for this week'}
        
        # Get current and previous week data
        current_week = week_data
        previous_week = historical_data[-7:] if len(historical_data) >= 7 else []
        
        # Calculate metrics
        latest = week_data[-1]
        earliest = week_data[0] if week_data else {}
        
        summary = {
            'current_stars': latest.get('stars', 0),
            'current_forks': latest.get('forks', 0),
            'current_watchers': latest.get('watchers', 0),
            'current_open_issues': latest.get('open_issues', 0),
            'current_closed_issues': latest.get('closed_issues', 0),
            'current_open_prs': latest.get('open_prs', 0),
            'current_merged_prs': latest.get('merged_prs', 0),
            'commits_this_week': latest.get('commits_last_week', 0),
            'contributors_count': latest.get('contributors_count', 0),
            'repository_size': latest.get('size', 0),
            'primary_language': latest.get('language', ''),
            'license': latest.get('license', ''),
            'created_at': latest.get('created_at', ''),
            'last_commit': latest.get('last_commit_date', '')
        }
        
        # Calculate growth rates
        star_growth = self.calculate_growth_rates(current_week, previous_week, 'stars')
        fork_growth = self.calculate_growth_rates(current_week, previous_week, 'forks')
        issue_growth = self.calculate_growth_rates(current_week, previous_week, 'open_issues')
        pr_growth = self.calculate_growth_rates(current_week, previous_week, 'merged_prs')
        
        summary.update({
            'stars_growth_rate': star_growth['growth_rate'],
            'forks_growth_rate': fork_growth['growth_rate'],
            'issues_growth_rate': issue_growth['growth_rate'],
            'pr_growth_rate': pr_growth['growth_rate']
        })
        
        # Calculate derived metrics
        if summary['current_open_issues'] + summary['current_closed_issues'] > 0:
            resolution_rate = (summary['current_closed_issues'] / 
                             (summary['current_open_issues'] + summary['current_closed_issues'])) * 100
            summary['issue_resolution_rate'] = resolution_rate
        else:
            summary['issue_resolution_rate'] = 0
        
        if summary['current_open_prs'] + summary['current_merged_prs'] > 0:
            merge_rate = (summary['current_merged_prs'] / 
                         (summary['current_open_prs'] + summary['current_merged_prs'])) * 100
            summary['pr_merge_rate'] = merge_rate
        else:
            summary['pr_merge_rate'] = 0
        
        return summary
    
    def generate_discord_summary(self, week_data: List[Dict],
                                historical_data: List[Dict]) -> Dict[str, Any]:
        """Generate Discord community summary"""
        if not week_data:
            return {'error': 'No Discord data available for this week'}
        
        # Get current and previous week data
        current_week = week_data
        previous_week = historical_data[-7:] if len(historical_data) >= 7 else []
        
        # Calculate metrics
        latest = week_data[-1]
        
        summary = {
            'total_members': latest.get('total_members', 0),
            'online_members': latest.get('online_members', 0),
            'daily_active_users': latest.get('daily_active_users', 0),
            'weekly_active_users': latest.get('weekly_active_users', 0),
            'monthly_active_users': latest.get('monthly_active_users', 0),
            'messages_today': latest.get('message_count_today', 0),
            'messages_this_week': latest.get('message_count_week', 0),
            'messages_this_month': latest.get('message_count_month', 0),
            'new_members_today': latest.get('new_members_today', 0),
            'new_members_this_week': latest.get('new_members_week', 0),
            'new_members_this_month': latest.get('new_members_month', 0)
        }
        
        # Calculate growth rates
        member_growth = self.calculate_growth_rates(current_week, previous_week, 'total_members')
        dau_growth = self.calculate_growth_rates(current_week, previous_week, 'daily_active_users')
        message_growth = self.calculate_growth_rates(current_week, previous_week, 'message_count_week')
        
        summary.update({
            'member_growth_rate': member_growth['growth_rate'],
            'dau_growth_rate': dau_growth['growth_rate'],
            'message_growth_rate': message_growth['growth_rate']
        })
        
        # Calculate derived metrics
        if summary['total_members'] > 0:
            online_rate = (summary['online_members'] / summary['total_members']) * 100
            summary['online_rate'] = online_rate
        else:
            summary['online_rate'] = 0
        
        if summary['total_members'] > 0:
            engagement_rate = (summary['daily_active_users'] / summary['total_members']) * 100
            summary['engagement_rate'] = engagement_rate
        else:
            summary['engagement_rate'] = 0
        
        return summary
    
    def generate_nuget_summary(self, week_data: List[Dict],
                              historical_data: List[Dict]) -> Dict[str, Any]:
        """Generate NuGet package summary"""
        if not week_data:
            return {'error': 'No NuGet data available for this week'}
        
        # Group data by package
        packages_summary = {}
        
        for week_item in week_data:
            package_id = week_item.get('package_id', 'unknown')
            if package_id not in packages_summary:
                packages_summary[package_id] = {
                    'package_id': package_id,
                    'total_downloads': 0,
                    'downloads_last_week': 0,
                    'downloads_last_month': 0,
                    'latest_version': week_item.get('latest_version', ''),
                    'last_updated': week_item.get('last_updated', '')
                }
            
            # Use the most recent data for each package
            packages_summary[package_id].update({
                'total_downloads': week_item.get('total_downloads', 0),
                'downloads_last_week': week_item.get('downloads_last_week', 0),
                'downloads_last_month': week_item.get('downloads_last_month', 0)
            })
        
        # Calculate growth rates for each package
        for package_id, package_data in packages_summary.items():
            package_historical = [item for item in historical_data 
                                if item.get('package_id') == package_id]
            
            if package_historical:
                previous_week_data = package_historical[-1] if package_historical else {}
                
                # Calculate download growth
                current_downloads = package_data['downloads_last_week']
                previous_downloads = previous_week_data.get('downloads_last_week', 0)
                
                if previous_downloads > 0:
                    growth_rate = ((current_downloads - previous_downloads) / previous_downloads) * 100
                else:
                    growth_rate = 100 if current_downloads > 0 else 0
                
                package_data['downloads_growth_rate'] = growth_rate
            else:
                package_data['downloads_growth_rate'] = 0
        
        return {
            'packages': list(packages_summary.values()),
            'total_packages': len(packages_summary),
            'total_downloads': sum(p['total_downloads'] for p in packages_summary.values()),
            'total_downloads_this_week': sum(p['downloads_last_week'] for p in packages_summary.values())
        }
    
    def generate_trends_analysis(self, historical_data: Dict[str, List]) -> Dict[str, Any]:
        """Analyze trends from historical data"""
        trends = {}
        
        # GitHub trends
        github_data = historical_data.get('github', [])
        if len(github_data) >= 14:  # At least 2 weeks of data
            # Calculate 2-week moving averages
            dates = [datetime.fromisoformat(item['timestamp']) for item in github_data]
            stars = [item.get('stars', 0) for item in github_data]
            forks = [item.get('forks', 0) for item in github_data]
            commits = [item.get('commits_last_week', 0) for item in github_data]
            
            # Calculate trends
            if len(stars) >= 2:
                star_trend = "Increasing" if stars[-1] > stars[0] else "Decreasing" if stars[-1] < stars[0] else "Stable"
                trends['github_stars'] = star_trend
            
            if len(forks) >= 2:
                fork_trend = "Increasing" if forks[-1] > forks[0] else "Decreasing" if forks[-1] < forks[0] else "Stable"
                trends['github_forks'] = fork_trend
            
            if len(commits) >= 2:
                commit_trend = "Increasing" if commits[-1] > commits[0] else "Decreasing" if commits[-1] < commits[0] else "Stable"
                trends['github_commits'] = commit_trend
        
        # Discord trends
        discord_data = historical_data.get('discord', [])
        if len(discord_data) >= 14:
            member_counts = [item.get('total_members', 0) for item in discord_data]
            dau_counts = [item.get('daily_active_users', 0) for item in discord_data]
            
            if len(member_counts) >= 2:
                member_trend = "Growing" if member_counts[-1] > member_counts[0] else "Declining" if member_counts[-1] < member_counts[0] else "Stable"
                trends['discord_members'] = member_trend
            
            if len(dau_counts) >= 2:
                dau_trend = "Improving" if dau_counts[-1] > dau_counts[0] else "Declining" if dau_counts[-1] < dau_counts[0] else "Stable"
                trends['discord_engagement'] = dau_trend
        
        # NuGet trends
        nuget_data = historical_data.get('nuget', [])
        if nuget_data:
            package_downloads = defaultdict(list)
            for item in nuget_data:
                package_id = item.get('package_id', 'unknown')
                package_downloads[package_id].append(item.get('total_downloads', 0))
            
            trends['nuget_downloads'] = {}
            for package_id, downloads in package_downloads.items():
                if len(downloads) >= 2:
                    trend = "Growing" if downloads[-1] > downloads[0] else "Declining" if downloads[-1] < downloads[0] else "Stable"
                    trends['nuget_downloads'][package_id] = trend
        
        return trends
    
    def generate_alerts_summary(self, alerts_data: List[Dict]) -> Dict[str, Any]:
        """Generate alerts summary"""
        if not alerts_data:
            return {'total_alerts': 0, 'by_severity': {}, 'by_type': {}}
        
        # Count alerts by severity and type
        by_severity = defaultdict(int)
        by_type = defaultdict(int)
        
        for alert in alerts_data:
            severity = alert.get('severity', 'unknown')
            alert_type = alert.get('type', 'unknown')
            
            by_severity[severity] += 1
            by_type[alert_type] += 1
        
        return {
            'total_alerts': len(alerts_data),
            'by_severity': dict(by_severity),
            'by_type': dict(by_type),
            'recent_alerts': alerts_data[-5:] if len(alerts_data) > 5 else alerts_data
        }
    
    def generate_recommendations(self, github_summary: Dict, discord_summary: Dict,
                                nuget_summary: Dict, trends: Dict, 
                                alerts: Dict) -> List[str]:
        """Generate actionable recommendations based on data"""
        recommendations = []
        
        # GitHub recommendations
        if github_summary.get('stars_growth_rate', 0) < 5:
            recommendations.append(
                "Consider increasing marketing efforts to boost GitHub star growth. "
                "Current growth rate is below 5%."
            )
        
        if github_summary.get('issue_resolution_rate', 100) < 80:
            recommendations.append(
                "Issue resolution rate is below 80%. Consider allocating more resources "
                "to issue triage and resolution."
            )
        
        if github_summary.get('commits_this_week', 0) < 5:
            recommendations.append(
                "Low commit activity this week. Consider promoting contribution "
                "opportunities or focusing on backlog items."
            )
        
        # Discord recommendations
        if discord_summary.get('engagement_rate', 100) < 30:
            recommendations.append(
                f"Discord engagement rate is {discord_summary.get('engagement_rate', 0):.1f}%. "
                "Consider organizing community events or active discussions."
            )
        
        if discord_summary.get('member_growth_rate', 0) < 2:
            recommendations.append(
                "Discord member growth is slow. Consider reaching out to existing "
                "community members for referrals or improving onboarding."
            )
        
        # NuGet recommendations
        if nuget_summary.get('packages'):
            for package in nuget_summary['packages']:
                if package.get('downloads_growth_rate', 0) < 0:
                    recommendations.append(
                        f"Download decline detected for {package['package_id']}. "
                        "Consider user feedback collection and feature improvements."
                    )
        
        # Alert-based recommendations
        critical_alerts = alerts.get('by_severity', {}).get('critical', 0)
        if critical_alerts > 0:
            recommendations.append(
                f"{critical_alerts} critical alerts were triggered this week. "
                "Immediate attention required for community health issues."
            )
        
        # Trend-based recommendations
        if trends.get('github_commits') == 'Declining':
            recommendations.append(
                "Declining commit trend detected. Consider investigating contributor "
                "satisfaction and removing barriers to contribution."
            )
        
        if trends.get('discord_engagement') == 'Declining':
            recommendations.append(
                "Discord engagement is declining. Consider scheduling regular "
                "community events or AMAs with maintainers."
            )
        
        # Default recommendations if no specific issues
        if not recommendations:
            recommendations.extend([
                "Community health appears stable this week. Continue current strategies.",
                "Consider conducting a community satisfaction survey to gather feedback.",
                "Look for opportunities to showcase community contributions and success stories."
            ])
        
        return recommendations[:5]  # Limit to top 5 recommendations
    
    def calculate_health_scores(self, github_summary: Dict, discord_summary: Dict,
                               nuget_summary: Dict) -> Dict[str, float]:
        """Calculate health scores for different areas"""
        scores = {}
        
        # GitHub health score
        github_score = 100.0
        
        # Deduct points for various issues
        if github_summary.get('stars_growth_rate', 0) < 0:
            github_score -= 20
        elif github_summary.get('stars_growth_rate', 0) < 5:
            github_score -= 10
        
        if github_summary.get('issue_resolution_rate', 100) < 80:
            github_score -= 25
        
        if github_summary.get('commits_this_week', 10) < 5:
            github_score -= 15
        
        if github_summary.get('pr_merge_rate', 100) < 70:
            github_score -= 15
        
        scores['github'] = max(0.0, github_score)
        
        # Discord health score
        discord_score = 100.0
        
        if discord_summary.get('engagement_rate', 100) < 30:
            discord_score -= 30
        elif discord_summary.get('engagement_rate', 100) < 50:
            discord_score -= 15
        
        if discord_summary.get('member_growth_rate', 0) < 0:
            discord_score -= 20
        
        if discord_summary.get('online_rate', 100) < 10:
            discord_score -= 25
        
        scores['discord'] = max(0.0, discord_score)
        
        # NuGet health score
        nuget_score = 100.0
        
        if nuget_summary.get('packages'):
            declining_packages = [p for p in nuget_summary['packages'] 
                                if p.get('downloads_growth_rate', 0) < 0]
            if declining_packages:
                nuget_score -= len(declining_packages) * 15
        
        scores['nuget'] = max(0.0, nuget_score)
        
        # Overall score
        scores['overall'] = statistics.mean([scores['github'], scores['discord'], scores['nuget']])
        
        return scores
    
    def create_visualizations(self, historical_data: Dict[str, List], 
                            output_dir: Path) -> List[str]:
        """Create visualization charts for the report"""
        chart_files = []
        
        # Set up matplotlib for non-interactive use
        import matplotlib
        matplotlib.use('Agg')
        
        # GitHub Stars Trend Chart
        github_data = historical_data.get('github', [])
        if len(github_data) >= 7:
            dates = [datetime.fromisoformat(item['timestamp']) for item in github_data]
            stars = [item.get('stars', 0) for item in github_data]
            
            plt.figure(figsize=(12, 6))
            plt.plot(dates, stars, marker='o', linewidth=2, markersize=4)
            plt.title('GitHub Stars Trend (Last 30 Days)', fontsize=16, fontweight='bold')
            plt.xlabel('Date', fontsize=12)
            plt.ylabel('Stars', fontsize=12)
            plt.grid(True, alpha=0.3)
            plt.xticks(rotation=45)
            
            # Format x-axis
            plt.gca().xaxis.set_major_formatter(mdates.DateFormatter('%m/%d'))
            plt.gca().xaxis.set_major_locator(mdates.WeekdayLocator())
            
            plt.tight_layout()
            chart_file = output_dir / 'github_stars_trend.png'
            plt.savefig(chart_file, dpi=300, bbox_inches='tight')
            plt.close()
            chart_files.append(str(chart_file))
        
        # Discord Members Trend Chart
        discord_data = historical_data.get('discord', [])
        if len(discord_data) >= 7:
            dates = [datetime.fromisoformat(item['timestamp']) for item in discord_data]
            total_members = [item.get('total_members', 0) for item in discord_data]
            dau = [item.get('daily_active_users', 0) for item in discord_data]
            
            fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(12, 8))
            
            # Total members
            ax1.plot(dates, total_members, marker='o', linewidth=2, color='blue', label='Total Members')
            ax1.set_title('Discord Community Growth', fontsize=14, fontweight='bold')
            ax1.set_ylabel('Total Members', fontsize=12)
            ax1.grid(True, alpha=0.3)
            ax1.legend()
            
            # Daily active users
            ax2.plot(dates, dau, marker='s', linewidth=2, color='green', label='Daily Active Users')
            ax2.set_title('Discord Engagement', fontsize=14, fontweight='bold')
            ax2.set_xlabel('Date', fontsize=12)
            ax2.set_ylabel('Daily Active Users', fontsize=12)
            ax2.grid(True, alpha=0.3)
            ax2.legend()
            
            # Format x-axis for both subplots
            for ax in [ax1, ax2]:
                ax.xaxis.set_major_formatter(mdates.DateFormatter('%m/%d'))
                ax.xaxis.set_major_locator(mdates.WeekdayLocator())
                plt.setp(ax.xaxis.get_majorticklabels(), rotation=45)
            
            plt.tight_layout()
            chart_file = output_dir / 'discord_trends.png'
            plt.savefig(chart_file, dpi=300, bbox_inches='tight')
            plt.close()
            chart_files.append(str(chart_file))
        
        # Health Score Dashboard
        github_summary = self.generate_github_summary(github_data[:7], github_data[:-7] if len(github_data) > 7 else [])
        discord_summary = self.generate_discord_summary(discord_data[:7], discord_data[:-7] if len(discord_data) > 7 else [])
        nuget_summary = self.generate_nuget_summary(historical_data.get('nuget', [])[:7], historical_data.get('nuget', [])[:-7] if len(historical_data.get('nuget', [])) > 7 else [])
        
        scores = self.calculate_health_scores(github_summary, discord_summary, nuget_summary)
        
        fig, ((ax1, ax2), (ax3, ax4)) = plt.subplots(2, 2, figsize=(12, 8))
        
        # GitHub Health
        self._create_gauge_chart(ax1, scores['github'], 'GitHub Health', 'blue')
        
        # Discord Health
        self._create_gauge_chart(ax2, scores['discord'], 'Discord Health', 'green')
        
        # NuGet Health
        self._create_gauge_chart(ax3, scores['nuget'], 'NuGet Health', 'orange')
        
        # Overall Health
        self._create_gauge_chart(ax4, scores['overall'], 'Overall Health', 'purple')
        
        plt.suptitle('TiXL Community Health Dashboard', fontsize=16, fontweight='bold')
        plt.tight_layout()
        chart_file = output_dir / 'health_dashboard.png'
        plt.savefig(chart_file, dpi=300, bbox_inches='tight')
        plt.close()
        chart_files.append(str(chart_file))
        
        return chart_files
    
    def _create_gauge_chart(self, ax, score: float, title: str, color: str):
        """Create a gauge chart for health scores"""
        # Create a semi-circle gauge
        theta = np.linspace(0, np.pi, 100)
        
        # Color coding
        if score >= 80:
            gauge_color = 'green'
        elif score >= 60:
            gauge_color = 'orange'
        else:
            gauge_color = 'red'
        
        # Draw gauge background
        ax.fill_between(theta, 0, 1, alpha=0.3, color='lightgray')
        
        # Draw score arc
        score_theta = np.linspace(0, np.pi * score / 100, int(score))
        ax.fill_between(score_theta, 0, 1, alpha=0.8, color=gauge_color)
        
        # Add score text
        ax.text(np.pi/2, 0.5, f'{score:.1f}', 
               fontsize=24, fontweight='bold', ha='center', va='center')
        ax.text(np.pi/2, 0.2, title, 
               fontsize=12, ha='center', va='center')
        
        ax.set_xlim(0, np.pi)
        ax.set_ylim(0, 1)
        ax.set_aspect('equal')
        ax.axis('off')
    
    def generate_report(self, week_start: datetime, week_end: datetime) -> WeeklyReportData:
        """Generate comprehensive weekly report"""
        logger.info(f"Generating weekly report for {week_start.date()} to {week_end.date()}")
        
        # Get data for the week
        week_data = self.db_manager.get_weekly_data(week_start, week_end)
        historical_data = self.db_manager.get_historical_data()
        
        # Generate summaries
        github_summary = self.generate_github_summary(
            week_data['github'], historical_data['github']
        )
        discord_summary = self.generate_discord_summary(
            week_data['discord'], historical_data['discord']
        )
        nuget_summary = self.generate_nuget_summary(
            week_data['nuget'], historical_data['nuget']
        )
        
        # Analyze trends
        trends = self.generate_trends_analysis(historical_data)
        
        # Generate alerts summary
        alerts_summary = self.generate_alerts_summary(week_data['alerts'])
        
        # Generate recommendations
        recommendations = self.generate_recommendations(
            github_summary, discord_summary, nuget_summary, trends, alerts_summary
        )
        
        # Calculate health scores
        health_scores = self.calculate_health_scores(
            github_summary, discord_summary, nuget_summary
        )
        
        # Create visualizations
        chart_files = self.create_visualizations(historical_data, self.output_dir)
        
        return WeeklyReportData(
            week_start=week_start.isoformat(),
            week_end=week_end.isoformat(),
            github_summary=github_summary,
            discord_summary=discord_summary,
            nuget_summary=nuget_summary,
            trends=trends,
            alerts=alerts_summary.get('recent_alerts', []),
            recommendations=recommendations,
            health_scores=health_scores
        )

class MarkdownReportGenerator:
    """Generate Markdown reports from weekly data"""
    
    def __init__(self, output_dir: Path):
        self.output_dir = output_dir
        self.output_dir.mkdir(parents=True, exist_ok=True)
    
    def format_number(self, num: float, decimals: int = 1) -> str:
        """Format numbers with appropriate suffixes"""
        if num >= 1000000:
            return f"{num/1000000:.1f}M"
        elif num >= 1000:
            return f"{num/1000:.1f}K"
        else:
            return f"{num:.{decimals}f}"
    
    def format_percentage(self, value: float, decimals: int = 1) -> str:
        """Format percentage values"""
        return f"{value:.{decimals}f}%"
    
    def generate_markdown_report(self, report_data: WeeklyReportData) -> str:
        """Generate comprehensive Markdown report"""
        
        week_start = datetime.fromisoformat(report_data.week_start).strftime('%B %d, %Y')
        week_end = datetime.fromisoformat(report_data.week_end).strftime('%B %d, %Y')
        
        report = f"""# TiXL Community Health Report
**Week of {week_start} to {week_end}**

## 📊 Executive Summary

### Overall Health Score: {report_data.health_scores['overall']:.1f}/100

| Area | Health Score | Status |
|------|-------------|---------|
| GitHub Repository | {report_data.health_scores['github']:.1f}/100 | {'🟢 Excellent' if report_data.health_scores['github'] >= 80 else '🟡 Good' if report_data.health_scores['github'] >= 60 else '🔴 Needs Attention'} |
| Discord Community | {report_data.health_scores['discord']:.1f}/100 | {'🟢 Excellent' if report_data.health_scores['discord'] >= 80 else '🟡 Good' if report_data.health_scores['discord'] >= 60 else '🔴 Needs Attention'} |
| NuGet Packages | {report_data.health_scores['nuget']:.1f}/100 | {'🟢 Excellent' if report_data.health_scores['nuget'] >= 80 else '🟡 Good' if report_data.health_scores['nuget'] >= 60 else '🔴 Needs Attention'} |

## 🚀 GitHub Repository Metrics

"""

        # GitHub Summary
        github_summary = report_data.github_summary
        if 'error' not in github_summary:
            report += f"""### Repository Overview
- **Stars:** {self.format_number(github_summary['current_stars'])} ({self.format_percentage(github_summary.get('stars_growth_rate', 0))} vs last week)
- **Forks:** {self.format_number(github_summary['current_forks'])} ({self.format_percentage(github_summary.get('forks_growth_rate', 0))} vs last week)
- **Watchers:** {self.format_number(github_summary['current_watchers'])}
- **Contributors:** {github_summary['contributors_count']}
- **Primary Language:** {github_summary.get('primary_language', 'N/A')}
- **License:** {github_summary.get('license', 'N/A')}

### Activity Metrics
- **Commits This Week:** {github_summary['commits_this_week']}
- **Open Issues:** {github_summary['current_open_issues']}
- **Closed Issues:** {github_summary['current_closed_issues']}
- **Issue Resolution Rate:** {self.format_percentage(github_summary.get('issue_resolution_rate', 0))}
- **Open PRs:** {github_summary['current_open_prs']}
- **Merged PRs:** {github_summary['current_merged_prs']}
- **PR Merge Rate:** {self.format_percentage(github_summary.get('pr_merge_rate', 0))}

### Repository Health
- **Size:** {self.format_number(github_summary['repository_size'])} KB
- **Created:** {datetime.fromisoformat(github_summary['created_at']).strftime('%B %d, %Y') if github_summary.get('created_at') else 'N/A'}
- **Last Commit:** {datetime.fromisoformat(github_summary['last_commit']).strftime('%B %d, %Y') if github_summary.get('last_commit') else 'N/A'}

"""
        else:
            report += f"❌ {github_summary['error']}\n\n"

        # Discord Summary
        discord_summary = report_data.discord_summary
        report += "## 💬 Discord Community Metrics\n\n"
        
        if 'error' not in discord_summary:
            report += f"""### Community Overview
- **Total Members:** {self.format_number(discord_summary['total_members'])} ({self.format_percentage(discord_summary.get('member_growth_rate', 0))} vs last week)
- **Online Members:** {self.format_number(discord_summary['online_members'])} ({self.format_percentage(discord_summary.get('online_rate', 0))} online rate)
- **Daily Active Users:** {self.format_number(discord_summary['daily_active_users'])} ({self.format_percentage(discord_summary.get('dau_growth_rate', 0))} vs last week)
- **Weekly Active Users:** {self.format_number(discord_summary['weekly_active_users'])}
- **Monthly Active Users:** {self.format_number(discord_summary['monthly_active_users'])}

### Engagement Metrics
- **Engagement Rate:** {self.format_percentage(discord_summary.get('engagement_rate', 0))}
- **Messages This Week:** {self.format_number(discord_summary['messages_this_week'])}
- **New Members This Week:** {discord_summary['new_members_this_week']}

"""
        else:
            report += f"❌ {discord_summary['error']}\n\n"

        # NuGet Summary
        nuget_summary = report_data.nuget_summary
        report += "## 📦 NuGet Package Metrics\n\n"
        
        if nuget_summary.get('packages'):
            report += f"""### Package Overview
- **Total Packages:** {nuget_summary['total_packages']}
- **Total Downloads:** {self.format_number(nuget_summary['total_downloads'])}
- **Downloads This Week:** {self.format_number(nuget_summary['total_downloads_this_week'])}

### Package Details
"""
            for package in nuget_summary['packages']:
                growth_rate = package.get('downloads_growth_rate', 0)
                status_emoji = "📈" if growth_rate > 0 else "📉" if growth_rate < 0 else "➡️"
                
                report += f"""- **{package['package_id']} v{package['latest_version']}**
  - Downloads: {self.format_number(package['total_downloads'])} ({self.format_percentage(growth_rate)})
  - Last Updated: {datetime.fromisoformat(package['last_updated']).strftime('%B %d, %Y') if package.get('last_updated') else 'N/A'}

"""
        else:
            report += f"❌ {nuget_summary.get('error', 'No NuGet data available')}\n\n"

        # Trends Analysis
        report += "## 📈 Trend Analysis\n\n"
        trends = report_data.trends
        
        if trends:
            for category, trend in trends.items():
                if isinstance(trend, dict):
                    for item, item_trend in trend.items():
                        icon = "📈" if item_trend == "Growing" else "📉" if item_trend == "Declining" else "➡️"
                        report += f"- **{category.replace('_', ' ').title()}:** {icon} {item_trend}\n"
                else:
                    icon = "📈" if trend == "Increasing" else "📉" if trend == "Decreasing" else "➡️"
                    report += f"- **{category.replace('_', ' ').title()}:** {icon} {trend}\n"
        else:
            report += "No significant trends identified this week.\n"
        
        report += "\n"

        # Alerts Summary
        alerts = report_data.alerts
        report += "## ⚠️ Alerts & Issues\n\n"
        
        if alerts:
            recent_alerts = alerts.get('recent_alerts', [])
            if recent_alerts:
                report += "### Recent Alerts\n"
                for alert in recent_alerts:
                    severity_emoji = "🚨" if alert.get('severity') == 'critical' else "⚠️" if alert.get('severity') == 'warning' else "ℹ️"
                    report += f"- {severity_emoji} **{alert.get('type', 'Unknown').title()}:** {alert.get('message', 'No message')}\n"
                
                report += f"\n### Alert Summary\n"
                by_severity = alerts.get('by_severity', {})
                for severity, count in by_severity.items():
                    emoji = "🚨" if severity == 'critical' else "⚠️" if severity == 'warning' else "ℹ️"
                    report += f"- {emoji} **{severity.title()}:** {count} alerts\n"
            else:
                report += "✅ No alerts generated this week.\n"
        else:
            report += "✅ No alerts generated this week.\n"
        
        report += "\n"

        # Recommendations
        recommendations = report_data.recommendations
        report += "## 💡 Recommendations\n\n"
        
        if recommendations:
            for i, rec in enumerate(recommendations, 1):
                report += f"{i}. {rec}\n"
        else:
            report += "No specific recommendations this week. Keep up the great work!\n"
        
        report += "\n"

        # Metrics Visualizations
        report += "## 📊 Visualizations\n\n"
        report += "![GitHub Stars Trend](../charts/github_stars_trend.png)\n\n"
        report += "![Discord Trends](../charts/discord_trends.png)\n\n"
        report += "![Health Dashboard](../charts/health_dashboard.png)\n\n"

        # Footer
        report += f"""---
*Report generated on {datetime.utcnow().strftime('%B %d, %Y at %H:%M UTC')}*
*TiXL Community Health Monitoring System*
"""

        return report
    
    def save_report(self, report_data: WeeklyReportData) -> Path:
        """Save report to file"""
        week_start = datetime.fromisoformat(report_data.week_start)
        filename = f"community_health_report_{week_start.strftime('%Y_%m_%d')}.md"
        filepath = self.output_dir / filename
        
        report_content = self.generate_markdown_report(report_data)
        
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(report_content)
        
        logger.info(f"Weekly report saved to {filepath}")
        return filepath

class WeeklyHealthReporter:
    """Main weekly reporting system"""
    
    def __init__(self, db_path: str = "data/community_health.db"):
        self.db_manager = DatabaseManager(db_path)
        self.report_generator = ReportGenerator(self.db_manager)
        self.markdown_generator = MarkdownReportGenerator(Path("reports/weekly"))
        self.charts_dir = Path("charts")
        self.charts_dir.mkdir(exist_ok=True)
    
    def generate_weekly_report(self, target_date: Optional[datetime] = None) -> Path:
        """Generate weekly report for specified date (defaults to last week)"""
        if target_date is None:
            target_date = datetime.utcnow() - timedelta(days=7)
        
        # Calculate week boundaries (Monday to Sunday)
        days_since_monday = target_date.weekday()
        week_start = target_date - timedelta(days=days_since_monday)
        week_start = week_start.replace(hour=0, minute=0, second=0, microsecond=0)
        week_end = week_start + timedelta(days=6, hours=23, minutes=59, seconds=59)
        
        logger.info(f"Generating weekly report for week starting {week_start.date()}")
        
        # Generate report data
        report_data = self.report_generator.generate_report(week_start, week_end)
        
        # Save Markdown report
        report_path = self.markdown_generator.save_report(report_data)
        
        # Save JSON data for programmatic access
        json_path = report_path.with_suffix('.json')
        with open(json_path, 'w', encoding='utf-8') as f:
            json.dump(report_data.__dict__, f, indent=2, default=str)
        
        logger.info(f"JSON data saved to {json_path}")
        
        return report_path

def main():
    """Main entry point"""
    import argparse
    
    parser = argparse.ArgumentParser(description='TiXL Weekly Community Health Reporter')
    parser.add_argument('--date', type=str,
                       help='Target date for weekly report (YYYY-MM-DD format)')
    parser.add_argument('--db-path', default='data/community_health.db',
                       help='Path to metrics database')
    parser.add_argument('--output-dir', default='reports/weekly',
                       help='Output directory for reports')
    
    args = parser.parse_args()
    
    # Parse target date
    if args.date:
        target_date = datetime.strptime(args.date, '%Y-%m-%d')
    else:
        target_date = None
    
    # Create reporter and generate report
    reporter = WeeklyHealthReporter(args.db_path)
    report_path = reporter.generate_weekly_report(target_date)
    
    print(f"Weekly report generated: {report_path}")

if __name__ == "__main__":
    main()