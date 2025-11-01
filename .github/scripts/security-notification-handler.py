#!/usr/bin/env python3
"""
TiXL Security Notification Handler
Sends security notifications based on scan results and gate status
"""

import json
import sys
import argparse
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Any, Optional
import logging
import smtplib
import ssl
from email.mime.text import MimeText
from email.mime.multipart import MimeMultipart
import requests

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class SecurityNotificationHandler:
    """Handles security notifications via multiple channels"""
    
    def __init__(self):
        self.notification_channels = {
            'email': self._send_email_notification,
            'slack': self._send_slack_notification,
            'teams': self._send_teams_notification,
            'webhook': self._send_webhook_notification
        }
    
    def process_notifications(self, scan_results_path: Path, gate_result: str, 
                            webhook_url: str = None, email_config: str = None, 
                            slack_config: str = None) -> None:
        """Process and send security notifications"""
        logger.info("Processing security notifications...")
        
        # Load scan results
        scan_data = self._load_scan_results(scan_results_path)
        
        # Determine notification urgency and channels
        urgency, channels = self._determine_notification_requirements(gate_result, scan_data)
        
        # Load channel configurations
        channel_configs = self._load_channel_configs(email_config, slack_config, webhook_url)
        
        # Send notifications based on urgency and channels
        for channel in channels:
            if channel in self.notification_channels:
                try:
                    self.notification_channels[channel](scan_data, urgency, channel_configs.get(channel, {}))
                    logger.info(f"Notification sent via {channel}")
                except Exception as e:
                    logger.error(f"Failed to send {channel} notification: {e}")
            else:
                logger.warning(f"Unknown notification channel: {channel}")
    
    def _load_scan_results(self, scan_results_path: Path) -> Dict[str, Any]:
        """Load security scan results"""
        scan_data = {
            'gate_status': 'UNKNOWN',
            'vulnerabilities': [],
            'severity_counts': {},
            'total_vulnerabilities': 0,
            'scan_timestamp': datetime.utcnow().isoformat()
        }
        
        # Load gate evaluation results
        gate_eval_files = list(scan_results_path.rglob("gate-evaluation*.json"))
        for gate_file in gate_eval_files:
            try:
                with open(gate_file, 'r', encoding='utf-8') as f:
                    gate_data = json.load(f)
                scan_data.update(gate_data)
                logger.info(f"Loaded gate evaluation from {gate_file}")
            except Exception as e:
                logger.error(f"Error loading gate evaluation: {e}")
        
        # Load vulnerability data
        vuln_files = list(scan_results_path.rglob("vulnerabilities.json"))
        for vuln_file in vuln_files:
            try:
                with open(vuln_file, 'r', encoding='utf-8') as f:
                    vulnerabilities = json.load(f)
                scan_data['vulnerabilities'] = vulnerabilities
                scan_data['total_vulnerabilities'] = len(vulnerabilities)
                
                # Calculate severity counts
                severity_counts = {}
                for vuln in vulnerabilities:
                    severity = vuln.get('severity', 'unknown')
                    severity_counts[severity] = severity_counts.get(severity, 0) + 1
                scan_data['severity_counts'] = severity_counts
                
                logger.info(f"Loaded {len(vulnerabilities)} vulnerabilities")
            except Exception as e:
                logger.error(f"Error loading vulnerabilities: {e}")
        
        return scan_data
    
    def _determine_notification_requirements(self, gate_result: str, scan_data: Dict[str, Any]) -> tuple:
        """Determine notification urgency and channels based on scan results"""
        
        # Critical situations requiring immediate notification
        if gate_result == 'failure':
            critical_count = scan_data.get('severity_counts', {}).get('critical', 0)
            if critical_count > 0:
                return 'critical', ['email', 'slack', 'teams', 'webhook']
            else:
                return 'high', ['email', 'slack', 'webhook']
        
        # High severity issues
        high_count = scan_data.get('severity_counts', {}).get('high', 0)
        if high_count > 5:
            return 'high', ['email', 'slack']
        
        # Medium severity issues
        medium_count = scan_data.get('severity_counts', {}).get('medium', 0)
        if medium_count > 10:
            return 'medium', ['slack', 'webhook']
        
        # Low severity - just info
        return 'info', ['webhook']
    
    def _load_channel_configs(self, email_config: str = None, slack_config: str = None, webhook_url: str = None) -> Dict[str, Dict]:
        """Load configuration for notification channels"""
        configs = {}
        
        # Email configuration
        if email_config:
            try:
                configs['email'] = json.loads(email_config)
            except json.JSONDecodeError:
                logger.warning("Invalid email configuration format")
        
        # Slack configuration
        if slack_config:
            try:
                configs['slack'] = json.loads(slack_config)
            except json.JSONDecodeError:
                logger.warning("Invalid slack configuration format")
        
        # Webhook configuration
        if webhook_url:
            configs['webhook'] = {'url': webhook_url}
        
        return configs
    
    def _send_email_notification(self, scan_data: Dict[str, Any], urgency: str, config: Dict[str, Any]) -> None:
        """Send email notification"""
        try:
            # Create email content
            subject, body = self._create_email_content(scan_data, urgency)
            
            # Email configuration (using environment variables or config)
            smtp_server = os.getenv('SMTP_SERVER', config.get('smtp_server', 'smtp.gmail.com'))
            smtp_port = int(os.getenv('SMTP_PORT', config.get('smtp_port', 587)))
            smtp_user = os.getenv('SMTP_USER', config.get('username'))
            smtp_password = os.getenv('SMTP_PASSWORD', config.get('password'))
            
            from_email = config.get('from', smtp_user)
            to_emails = config.get('to', ['security-team@example.com'])
            
            # Create message
            msg = MimeMultipart()
            msg['From'] = from_email
            msg['To'] = ', '.join(to_emails)
            msg['Subject'] = subject
            
            msg.attach(MimeText(body, 'html'))
            
            # Send email
            context = ssl.create_default_context()
            with smtplib.SMTP(smtp_server, smtp_port) as server:
                server.starttls(context=context)
                server.login(smtp_user, smtp_password)
                server.send_message(msg)
            
            logger.info(f"Email notification sent to {len(to_emails)} recipients")
            
        except Exception as e:
            logger.error(f"Failed to send email notification: {e}")
    
    def _send_slack_notification(self, scan_data: Dict[str, Any], urgency: str, config: Dict[str, Any]) -> None:
        """Send Slack notification"""
        try:
            # Create Slack content
            slack_content = self._create_slack_content(scan_data, urgency)
            
            webhook_url = config.get('webhook_url') or os.getenv('SLACK_WEBHOOK_URL')
            if not webhook_url:
                logger.warning("Slack webhook URL not configured")
                return
            
            # Send to Slack
            response = requests.post(webhook_url, json=slack_content)
            response.raise_for_status()
            
            logger.info("Slack notification sent successfully")
            
        except Exception as e:
            logger.error(f"Failed to send Slack notification: {e}")
    
    def _send_teams_notification(self, scan_data: Dict[str, Any], urgency: str, config: Dict[str, Any]) -> None:
        """Send Microsoft Teams notification"""
        try:
            # Create Teams content
            teams_content = self._create_teams_content(scan_data, urgency)
            
            webhook_url = config.get('webhook_url') or os.getenv('TEAMS_WEBHOOK_URL')
            if not webhook_url:
                logger.warning("Teams webhook URL not configured")
                return
            
            # Send to Teams
            response = requests.post(webhook_url, json=teams_content)
            response.raise_for_status()
            
            logger.info("Teams notification sent successfully")
            
        except Exception as e:
            logger.error(f"Failed to send Teams notification: {e}")
    
    def _send_webhook_notification(self, scan_data: Dict[str, Any], urgency: str, config: Dict[str, Any]) -> None:
        """Send generic webhook notification"""
        try:
            webhook_url = config.get('url') or os.getenv('SECURITY_WEBHOOK_URL')
            if not webhook_url:
                logger.warning("Webhook URL not configured")
                return
            
            # Create webhook payload
            webhook_payload = {
                'timestamp': datetime.utcnow().isoformat(),
                'urgency': urgency,
                'scan_data': scan_data,
                'source': 'tixl-security-scanner'
            }
            
            # Send webhook
            response = requests.post(webhook_url, json=webhook_payload)
            response.raise_for_status()
            
            logger.info("Webhook notification sent successfully")
            
        except Exception as e:
            logger.error(f"Failed to send webhook notification: {e}")
    
    def _create_email_content(self, scan_data: Dict[str, Any], urgency: str) -> tuple:
        """Create email subject and body content"""
        
        # Determine subject based on urgency
        urgency_emojis = {
            'critical': '🚨',
            'high': '⚠️',
            'medium': '⚡',
            'info': 'ℹ️'
        }
        
        emoji = urgency_emojis.get(urgency, 'ℹ️')
        gate_status = scan_data.get('gate_status', 'UNKNOWN')
        
        subject = f"{emoji} TiXL Security Alert - {urgency.upper()} - Gate: {gate_status}"
        
        # Create HTML body
        body = f"""
        <html>
        <body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;">
            <div style="max-width: 600px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);">
                <h1 style="color: #d73a49;">🔒 TiXL Security Alert</h1>
                
                <div style="background-color: #f6f8fa; padding: 20px; border-radius: 6px; margin: 20px 0;">
                    <h2 style="margin: 0; color: #24292e;">Alert Details</h2>
                    <p><strong>Urgency:</strong> {urgency.upper()}</p>
                    <p><strong>Gate Status:</strong> {gate_status}</p>
                    <p><strong>Total Vulnerabilities:</strong> {scan_data.get('total_vulnerabilities', 0)}</p>
                    <p><strong>Scan Timestamp:</strong> {scan_data.get('scan_timestamp', 'Unknown')}</p>
                </div>
                
                <h3 style="color: #24292e;">Vulnerability Breakdown</h3>
                <table style="width: 100%; border-collapse: collapse; margin: 20px 0;">
                    <tr style="background-color: #f6f8fa;">
                        <th style="padding: 10px; text-align: left; border: 1px solid #ddd;">Severity</th>
                        <th style="padding: 10px; text-align: left; border: 1px solid #ddd;">Count</th>
                    </tr>
        """
        
        severity_colors = {
            'critical': '#d73a49',
            'high': '#fb8500',
            'medium': '#ffb703',
            'low': '#28a745'
        }
        
        for severity, count in scan_data.get('severity_counts', {}).items():
            color = severity_colors.get(severity, '#6c757d')
            body += f"""
                    <tr>
                        <td style="padding: 10px; border: 1px solid #ddd;">
                            <span style="color: {color}; font-weight: bold;">{severity.title()}</span>
                        </td>
                        <td style="padding: 10px; border: 1px solid #ddd;">{count}</td>
                    </tr>
            """
        
        body += """
                </table>
                
                <div style="background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 6px; margin: 20px 0;">
                    <h4 style="margin: 0; color: #856404;">Recommended Actions</h4>
                    <ul style="margin: 10px 0; color: #856404;">
        """
        
        if urgency == 'critical':
            body += """
                        <li>Immediately investigate and address critical vulnerabilities</li>
                        <li>Stop all deployments until issues are resolved</li>
                        <li>Activate incident response procedures</li>
                        <li>Notify stakeholders and security team</li>
            """
        elif urgency == 'high':
            body += """
                        <li>Address high-severity issues within 72 hours</li>
                        <li>Schedule emergency security patch deployment</li>
                        <li>Review and update security policies</li>
            """
        else:
            body += """
                        <li>Review vulnerabilities during next sprint planning</li>
                        <li>Prioritize based on business impact</li>
                        <li>Plan systematic remediation</li>
            """
        
        body += """
                    </ul>
                </div>
                
                <p style="color: #586069; font-size: 14px; margin-top: 30px;">
                    This alert was generated by the TiXL Security Scanning Pipeline.<br>
                    For more details, check the security dashboard and scan artifacts.
                </p>
            </div>
        </body>
        </html>
        """
        
        return subject, body
    
    def _create_slack_content(self, scan_data: Dict[str, Any], urgency: str) -> Dict[str, Any]:
        """Create Slack message content"""
        
        gate_status = scan_data.get('gate_status', 'UNKNOWN')
        total_vulns = scan_data.get('total_vulnerabilities', 0)
        
        # Determine color based on urgency
        colors = {
            'critical': 'danger',
            'high': 'warning', 
            'medium': '#ffeb3b',
            'info': 'good'
        }
        
        color = colors.get(urgency, '#ffeb3b')
        
        # Create attachment with vulnerability details
        attachment = {
            "color": color,
            "title": f"🔒 TiXL Security Alert - {urgency.upper()}",
            "fields": [
                {"title": "Gate Status", "value": gate_status, "short": True},
                {"title": "Total Vulnerabilities", "value": str(total_vulns), "short": True}
            ]
        }
        
        # Add severity breakdown
        severity_text = []
        for severity, count in scan_data.get('severity_counts', {}).items():
            if count > 0:
                severity_text.append(f"{severity.title()}: {count}")
        
        if severity_text:
            attachment["fields"].append({
                "title": "Severity Breakdown",
                "value": "\n".join(severity_text),
                "short": False
            })
        
        # Add vulnerability count field
        critical_count = scan_data.get('severity_counts', {}).get('critical', 0)
        if critical_count > 0:
            attachment["fields"].append({
                "title": "🚨 Critical Issues",
                "value": str(critical_count),
                "short": True
            })
        
        return {
            "text": f"TiXL Security Scan Alert - {urgency.upper()} urgency detected",
            "attachments": [attachment]
        }
    
    def _create_teams_content(self, scan_data: Dict[str, Any], urgency: str) -> Dict[str, Any]:
        """Create Microsoft Teams message content"""
        
        # Determine theme color based on urgency
        theme_colors = {
            'critical': 'FF0000',  # Red
            'high': 'FFA500',      # Orange
            'medium': 'FFFF00',    # Yellow
            'info': '00FF00'       # Green
        }
        
        theme_color = theme_colors.get(urgency, '808080')
        
        gate_status = scan_data.get('gate_status', 'UNKNOWN')
        total_vulns = scan_data.get('total_vulnerabilities', 0)
        
        return {
            "@type": "MessageCard",
            "@context": "http://schema.org/extensions",
            "themeColor": theme_color,
            "summary": f"TiXL Security Alert - {urgency.upper()}",
            "title": f"🔒 TiXL Security Alert - {urgency.upper()}",
            "sections": [
                {
                    "activityTitle": "Security Scan Results",
                    "activitySubtitle": f"Gate Status: {gate_status}",
                    "facts": [
                        {"name": "Total Vulnerabilities", "value": str(total_vulns)},
                        {"name": "Urgency Level", "value": urgency.upper()},
                        {"name": "Scan Time", "value": scan_data.get('scan_timestamp', 'Unknown')}
                    ],
                    "markdown": True
                }
            ]
        }

def main():
    parser = argparse.ArgumentParser(description='TiXL Security Notification Handler')
    parser.add_argument('--scan-results', type=Path, required=True,
                       help='Directory containing security scan results')
    parser.add_argument('--gate-result', type=str, required=True,
                       choices=['success', 'failure'],
                       help='Result of security gate evaluation')
    parser.add_argument('--webhook-url', type=str, default=None,
                       help='Generic webhook URL for notifications')
    parser.add_argument('--email-config', type=str, default=None,
                       help='JSON configuration for email notifications')
    parser.add_argument('--slack-config', type=str, default=None,
                       help='JSON configuration for Slack notifications')
    
    args = parser.parse_args()
    
    try:
        handler = SecurityNotificationHandler()
        handler.process_notifications(
            scan_results_path=args.scan_results,
            gate_result=args.gate_result,
            webhook_url=args.webhook_url,
            email_config=args.email_config,
            slack_config=args.slack_config
        )
        
        logger.info("Security notifications processed successfully")
        sys.exit(0)
        
    except Exception as e:
        logger.error(f"Error processing security notifications: {e}")
        sys.exit(1)

if __name__ == '__main__':
    main()