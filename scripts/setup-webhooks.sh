#!/bin/bash
# TiXL Secret Scanning Webhook Setup Script
# This script sets up comprehensive webhook integration for secret scanning alerts

set -e

echo "🔗 Setting up TiXL Secret Scanning Webhook Integration"
echo "=================================================="

# Configuration
REPO_OWNER="${1:-tixl-project}"
REPO_NAME="${2:-tixl}"
GITHUB_TOKEN="${GITHUB_TOKEN:-}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

# Check prerequisites
check_prerequisites() {
    print_info "Checking prerequisites..."
    
    # Check if gh CLI is installed
    if ! command -v gh &> /dev/null; then
        print_error "GitHub CLI (gh) is required but not installed"
        echo "Install from: https://cli.github.com/"
        exit 1
    fi
    
    # Check if authenticated
    if ! gh auth status &> /dev/null; then
        print_error "Not authenticated with GitHub"
        echo "Run: gh auth login"
        exit 1
    fi
    
    # Check if jq is installed
    if ! command -v jq &> /dev/null; then
        print_warning "jq is recommended for JSON processing"
        echo "Install with: sudo apt-get install jq (Ubuntu) or brew install jq (macOS)"
    fi
    
    print_status "Prerequisites check completed"
}

# Create webhook configuration
create_webhook_config() {
    print_info "Creating webhook configuration..."
    
    mkdir -p .github/webhooks
    
    # Main webhook configuration
    cat > .github/webhooks/webhook-config.json << EOF
{
  "webhooks": [
    {
      "name": "TiXL Secret Scanning Alerts",
      "url": "\${SLACK_WEBHOOK_URL}",
      "events": [
        "secret_scanning_alert",
        "secret_scanning_push_protection",
        "security_advisory"
      ],
      "active": true,
      "content_type": "application/json",
      "config": {
        "insecure_ssl": "0"
      }
    },
    {
      "name": "Critical Security Alerts",
      "url": "\${MS_TEAMS_WEBHOOK_URL}",
      "events": [
        "security_advisory",
        "repository_vulnerability_alert"
      ],
      "active": true,
      "content_type": "application/json"
    },
    {
      "name": "Email Security Notifications",
      "url": "\${SECURITY_EMAIL_WEBHOOK}",
      "events": [
        "secret_scanning_alert",
        "security_advisory",
        "repository_vulnerability_alert"
      ],
      "active": true,
      "content_type": "application/json"
    }
  ],
  "secrets": {
    "SLACK_WEBHOOK_URL": "https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK",
    "MS_TEAMS_WEBHOOK_URL": "https://outlook.office.com/webhook/YOUR/TEAMS/WEBHOOK",
    "SECURITY_EMAIL_WEBHOOK": "smtp://your-smtp-server.com",
    "DISCORD_WEBHOOK_URL": "https://discord.com/api/webhooks/YOUR/DISCORD/WEBHOOK"
  },
  "notifications": {
    "critical": {
      "immediate": ["slack", "teams", "email"],
      "after_15m": ["email"],
      "after_1h": ["email"]
    },
    "high": {
      "immediate": ["slack", "teams"],
      "after_30m": ["email"]
    },
    "medium": {
      "immediate": ["slack"],
      "after_1h": ["email"]
    },
    "low": {
      "immediate": ["slack"],
      "daily_digest": true
    }
  }
}
EOF

    # Individual webhook payload templates
    mkdir -p .github/webhooks/templates
    
    # Slack template
    cat > .github/webhooks/templates/slack.json << 'EOF'
{
  "channel": "#security-alerts",
  "username": "TiXL Security Bot",
  "icon_emoji": ":shield:",
  "attachments": [
    {
      "color": "danger",
      "title": "🚨 Secret Exposure Alert",
      "text": "A potential secret exposure has been detected in the TiXL repository.",
      "fields": [
        {
          "title": "Repository",
          "value": "${{ github.repository }}",
          "short": true
        },
        {
          "title": "Branch",
          "value": "${{ github.ref_name }}",
          "short": true
        },
        {
          "title": "Severity",
          "value": "${{ webhook_event }}",
          "short": true
        },
        {
          "title": "Timestamp",
          "value": "${{ github.event.head_commit.timestamp }}",
          "short": true
        }
      ],
      "actions": [
        {
          "type": "button",
          "text": "View Repository",
          "url": "https://github.com/${{ github.repository }}"
        },
        {
          "type": "button",
          "text": "View Security Alerts",
          "url": "https://github.com/${{ github.repository }}/security"
        }
      ],
      "footer": "TiXL Secret Scanning System",
      "ts": ${{ github.event.head_commit.timestamp | epoch }}
    }
  ]
}
EOF

    # Microsoft Teams template
    cat > .github/webhooks/templates/teams.json << 'EOF'
{
  "@type": "MessageCard",
  "@context": "https://schema.org/extensions",
  "themeColor": "E81123",
  "summary": "TiXL Secret Exposure Alert",
  "sections": [
    {
      "activityTitle": "🚨 Secret Exposure Detected",
      "activitySubtitle": "TiXL Security Alert",
      "activityImage": "https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png",
      "facts": [
        {
          "name": "Repository",
          "value": "${{ github.repository }}"
        },
        {
          "name": "Branch",
          "value": "${{ github.ref_name }}"
        },
        {
          "name": "Commit",
          "value": "${{ github.sha }}"
        },
        {
          "name": "Severity",
          "value": "Critical"
        },
        {
          "name": "Timestamp",
          "value": "${{ github.event.head_commit.timestamp }}"
        }
      ],
      "markdown": true
    }
  ],
  "potentialAction": [
    {
      "@type": "OpenUri",
      "name": "View Repository",
      "targets": [
        {
          "os": "default",
          "uri": "https://github.com/${{ github.repository }}"
        }
      ]
    },
    {
      "@type": "OpenUri",
      "name": "View Security Alerts",
      "targets": [
        {
          "os": "default",
          "uri": "https://github.com/${{ github.repository }}/security"
        }
      ]
    }
  ]
}
EOF

    # Email template
    cat > .github/webhooks/templates/email.html << 'EOF'
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>TiXL Security Alert</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f6f8fa; }
        .container { max-width: 600px; margin: 0 auto; background-color: white; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .header { background-color: #dc3545; color: white; padding: 20px; border-radius: 8px 8px 0 0; }
        .content { padding: 20px; }
        .alert { border-left: 4px solid #dc3545; padding: 15px; margin: 20px 0; background-color: #f8d7da; }
        .button { display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 4px; margin: 10px 5px; }
        .footer { padding: 20px; background-color: #f8f9fa; border-radius: 0 0 8px 8px; font-size: 12px; color: #6c757d; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🚨 TiXL Security Alert</h1>
            <p>Secret Exposure Detected</p>
        </div>
        <div class="content">
            <div class="alert">
                <strong>Critical Security Alert:</strong> A potential secret exposure has been detected in the TiXL repository.
            </div>
            
            <h2>Alert Details</h2>
            <ul>
                <li><strong>Repository:</strong> ${{ github.repository }}</li>
                <li><strong>Branch:</strong> ${{ github.ref_name }}</li>
                <li><strong>Commit:</strong> ${{ github.sha }}</li>
                <li><strong>Timestamp:</strong> ${{ github.event.head_commit.timestamp }}</li>
                <li><strong>Severity:</strong> High</li>
            </ul>
            
            <h2>Required Actions</h2>
            <ol>
                <li>Immediately review the repository for exposed secrets</li>
                <li>Revoke any compromised credentials</li>
                <li>Follow incident response procedures</li>
                <li>Document the incident</li>
            </ol>
            
            <a href="https://github.com/${{ github.repository }}" class="button">View Repository</a>
            <a href="https://github.com/${{ github.repository }}/security" class="button">View Security Alerts</a>
        </div>
        <div class="footer">
            <p>This is an automated security alert from the TiXL Secret Scanning System.</p>
            <p>If you received this in error, please contact the security team.</p>
        </div>
    </div>
</body>
</html>
EOF

    print_status "Webhook configuration created"
}

# Setup GitHub webhooks
setup_github_webhooks() {
    print_info "Setting up GitHub repository webhooks..."
    
    # Repository webhooks
    gh api "repos/$REPO_OWNER/$REPO_NAME/hooks" \
        --method POST \
        --field name='web' \
        --field active=true \
        --field events='["secret_scanning_alert","security_advisory","repository_vulnerability_alert"]' \
        --field config.url='https://your-webhook-server.com/webhook' \
        --field config.content_type='json' \
        --field config.insecure_ssl='0' || print_warning "Failed to create repository webhook (may already exist)"
    
    # Organization webhooks (if running from organization)
    gh api "orgs/$REPO_OWNER/hooks" \
        --method POST \
        --field name='web' \
        --field active=true \
        --field events='["secret_scanning_alert","security_advisory"]' \
        --field config.url='https://your-webhook-server.com/org-webhook' \
        --field config.content_type='json' || print_warning "Failed to create organization webhook"
    
    print_status "GitHub webhooks configured"
}

# Create webhook server (simplified)
create_webhook_server() {
    print_info "Creating webhook server script..."
    
    cat > scripts/webhook-server.py << 'EOF'
#!/usr/bin/env python3
"""
TiXL Secret Scanning Webhook Server
Receives GitHub webhooks and forwards to configured notification channels
"""

import os
import json
import requests
import hmac
import hashlib
from flask import Flask, request, jsonify
from datetime import datetime

app = Flask(__name__)

# Configuration
SLACK_WEBHOOK_URL = os.environ.get('SLACK_WEBHOOK_URL')
MS_TEAMS_WEBHOOK_URL = os.environ.get('MS_TEAMS_WEBHOOK_URL')
DISCORD_WEBHOOK_URL = os.environ.get('DISCORD_WEBHOOK_URL')
EMAIL_WEBHOOK_URL = os.environ.get('EMAIL_WEBHOOK_URL')
GITHUB_WEBHOOK_SECRET = os.environ.get('GITHUB_WEBHOOK_SECRET', '')

def verify_github_signature(payload_body, signature_header):
    """Verify GitHub webhook signature"""
    if not GITHUB_WEBHOOK_SECRET:
        return True  # Skip verification if no secret configured
    
    expected_signature = 'sha256=' + hmac.new(
        GITHUB_WEBHOOK_SECRET.encode('utf-8'),
        payload_body,
        hashlib.sha256
    ).hexdigest()
    
    return hmac.compare_digest(expected_signature, signature_header or '')

def send_slack_notification(payload):
    """Send notification to Slack"""
    if not SLACK_WEBHOOK_URL:
        return False
    
    try:
        response = requests.post(SLACK_WEBHOOK_URL, json=payload)
        return response.status_code == 200
    except Exception as e:
        print(f"Failed to send Slack notification: {e}")
        return False

def send_teams_notification(payload):
    """Send notification to Microsoft Teams"""
    if not MS_TEAMS_WEBHOOK_URL:
        return False
    
    try:
        response = requests.post(MS_TEAMS_WEBHOOK_URL, json=payload)
        return response.status_code == 200
    except Exception as e:
        print(f"Failed to send Teams notification: {e}")
        return False

def send_discord_notification(payload):
    """Send notification to Discord"""
    if not DISCORD_WEBHOOK_URL:
        return False
    
    try:
        response = requests.post(DISCORD_WEBHOOK_URL, json=payload)
        return response.status_code == 200
    except Exception as e:
        print(f"Failed to send Discord notification: {e}")
        return False

def format_github_event(event_type, payload):
    """Format GitHub event for notifications"""
    base_info = {
        'repository': payload.get('repository', {}).get('full_name', 'Unknown'),
        'sender': payload.get('sender', {}).get('login', 'Unknown'),
        'timestamp': datetime.utcnow().isoformat() + 'Z'
    }
    
    if event_type == 'secret_scanning_alert':
        return {
            **base_info,
            'title': '🔒 Secret Scanning Alert',
            'description': f"Secret exposure detected in {base_info['repository']}",
            'severity': 'critical'
        }
    elif event_type == 'security_advisory':
        return {
            **base_info,
            'title': '⚠️ Security Advisory',
            'description': f"Security advisory published for {base_info['repository']}",
            'severity': 'high'
        }
    
    return base_info

@app.route('/webhook', methods=['POST'])
def webhook():
    """Main webhook endpoint"""
    signature = request.headers.get('X-Hub-Signature-256')
    event_type = request.headers.get('X-GitHub-Event')
    payload = request.get_json()
    
    # Verify signature
    if not verify_github_signature(request.data, signature):
        return jsonify({'error': 'Invalid signature'}), 401
    
    # Log the event
    print(f"Received GitHub event: {event_type}")
    
    # Format notification
    notification = format_github_event(event_type, payload)
    
    # Send notifications based on severity
    notifications_sent = []
    
    if notification['severity'] == 'critical':
        # Critical alerts go to all channels
        notifications_sent.append(('Slack', send_slack_notification(notification)))
        notifications_sent.append(('Teams', send_teams_notification(notification)))
        notifications_sent.append(('Discord', send_discord_notification(notification)))
    elif notification['severity'] == 'high':
        # High severity goes to Slack and Teams
        notifications_sent.append(('Slack', send_slack_notification(notification)))
        notifications_sent.append(('Teams', send_teams_notification(notification)))
    else:
        # Lower severity goes to Slack only
        notifications_sent.append(('Slack', send_slack_notification(notification)))
    
    # Log results
    for channel, success in notifications_sent:
        status = "✅ Success" if success else "❌ Failed"
        print(f"{channel} notification: {status}")
    
    return jsonify({'status': 'notifications_sent', 'count': len(notifications_sent)}), 200

@app.route('/health', methods=['GET'])
def health():
    """Health check endpoint"""
    return jsonify({
        'status': 'healthy',
        'timestamp': datetime.utcnow().isoformat() + 'Z'
    })

if __name__ == '__main__':
    port = int(os.environ.get('PORT', 5000))
    app.run(host='0.0.0.0', port=port, debug=False)
EOF

    chmod +x scripts/webhook-server.py
    
    print_status "Webhook server script created"
}

# Create deployment configuration
create_deployment_config() {
    print_info "Creating webhook deployment configuration..."
    
    # Docker configuration
    cat > Dockerfile.webhook << 'EOF'
FROM python:3.11-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY scripts/webhook-server.py .

EXPOSE 5000

CMD ["python", "webhook-server.py"]
EOF

    # Docker Compose configuration
    cat > docker-compose.webhook.yml << 'EOF'
version: '3.8'

services:
  webhook-server:
    build:
      context: .
      dockerfile: Dockerfile.webhook
    ports:
      - "5000:5000"
    environment:
      - SLACK_WEBHOOK_URL=${SLACK_WEBHOOK_URL}
      - MS_TEAMS_WEBHOOK_URL=${MS_TEAMS_WEBHOOK_URL}
      - DISCORD_WEBHOOK_URL=${DISCORD_WEBHOOK_URL}
      - EMAIL_WEBHOOK_URL=${EMAIL_WEBHOOK_URL}
      - GITHUB_WEBHOOK_SECRET=${GITHUB_WEBHOOK_SECRET}
      - PORT=5000
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
EOF

    # Kubernetes configuration
    cat > k8s/webhook-deployment.yml << 'EOF'
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tixl-webhook-server
  labels:
    app: tixl-webhook
spec:
  replicas: 2
  selector:
    matchLabels:
      app: tixl-webhook
  template:
    metadata:
      labels:
        app: tixl-webhook
    spec:
      containers:
      - name: webhook-server
        image: tixl/webhook-server:latest
        ports:
        - containerPort: 5000
        env:
        - name: SLACK_WEBHOOK_URL
          valueFrom:
            secretKeyRef:
              name: tixl-webhook-secrets
              key: slack-webhook
        - name: MS_TEAMS_WEBHOOK_URL
          valueFrom:
            secretKeyRef:
              name: tixl-webhook-secrets
              key: teams-webhook
        - name: GITHUB_WEBHOOK_SECRET
          valueFrom:
            secretKeyRef:
              name: tixl-webhook-secrets
              key: github-secret
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "200m"
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: tixl-webhook-service
spec:
  selector:
    app: tixl-webhook
  ports:
  - protocol: TCP
    port: 80
    targetPort: 5000
  type: LoadBalancer
EOF

    # Cloud Run configuration
    cat > cloudrun-webhook.yaml << 'EOF'
apiVersion: serving.knative.dev/v1
kind: Service
metadata:
  name: tixl-webhook-server
  annotations:
    run.googleapis.com/ingress: all
spec:
  template:
    metadata:
      annotations:
        autoscaling.knative.dev/maxScale: "10"
        run.googleapis.com/cpu-throttling: "false"
        run.googleapis.com/memory: "512Mi"
    spec:
      containerConcurrency: 80
      timeoutSeconds: 300
      containers:
      - image: gcr.io/tixl-project/webhook-server:latest
        ports:
        - containerPort: 5000
        env:
        - name: SLACK_WEBHOOK_URL
          valueFrom:
            secretKeyRef:
              name: tixl-webhook-secrets
              key: slack-webhook
        - name: MS_TEAMS_WEBHOOK_URL
          valueFrom:
            secretKeyRef:
              name: tixl-webhook-secrets
              key: teams-webhook
        - name: GITHUB_WEBHOOK_SECRET
          valueFrom:
            secretKeyRef:
              name: tixl-webhook-secrets
              key: github-secret
        resources:
          limits:
            cpu: "1000m"
            memory: "512Mi"
EOF

    print_status "Deployment configurations created"
}

# Create monitoring and alerting
create_monitoring_config() {
    print_info "Creating monitoring configuration..."
    
    cat > .github/webhooks/monitoring.json << 'EOF'
{
  "monitoring": {
    "enabled": true,
    "endpoints": {
      "health": "/health",
      "metrics": "/metrics",
      "webhook": "/webhook"
    },
    "alerts": {
      "webhook_failures": {
        "threshold": 3,
        "window": "5m",
        "actions": ["email", "slack"]
      },
      "high_latency": {
        "threshold": 5000,
        "window": "1m",
        "actions": ["slack"]
      },
      "error_rate": {
        "threshold": 0.1,
        "window": "2m",
        "actions": ["email"]
      }
    },
    "metrics": {
      "requests_total": true,
      "request_duration": true,
      "notification_success_rate": true,
      "error_rate": true
    }
  },
  "logging": {
    "level": "INFO",
    "structured": true,
    "retention_days": 30,
    "outputs": ["console", "file", "cloudwatch"]
  }
}
EOF

    print_status "Monitoring configuration created"
}

# Generate documentation
generate_documentation() {
    print_info "Generating webhook documentation..."
    
    cat > .github/webhooks/README.md << 'EOF'
# TiXL Secret Scanning Webhook Integration

This directory contains webhook configuration and setup for TiXL secret scanning alerts.

## Overview

The webhook integration provides real-time notifications when secret exposure events occur in the TiXL repository. Notifications are sent to multiple channels including Slack, Microsoft Teams, Discord, and email.

## Setup Instructions

### 1. Configure Webhook URLs

Edit `.github/webhooks/webhook-config.json` and update the webhook URLs:

```json
{
  "secrets": {
    "SLACK_WEBHOOK_URL": "https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK",
    "MS_TEAMS_WEBHOOK_URL": "https://outlook.office.com/webhook/YOUR/TEAMS/WEBHOOK",
    "SECURITY_EMAIL_WEBHOOK": "smtp://your-smtp-server.com",
    "DISCORD_WEBHOOK_URL": "https://discord.com/api/webhooks/YOUR/DISCORD/WEBHOOK"
  }
}
```

### 2. Deploy Webhook Server

#### Using Docker

```bash
# Build and run
docker build -f Dockerfile.webhook -t tixl/webhook-server .
docker run -p 5000:5000 \
  -e SLACK_WEBHOOK_URL="your-slack-url" \
  -e MS_TEAMS_WEBHOOK_URL="your-teams-url" \
  -e GITHUB_WEBHOOK_SECRET="your-github-secret" \
  tixl/webhook-server
```

#### Using Docker Compose

```bash
# Set environment variables
export SLACK_WEBHOOK_URL="your-slack-url"
export MS_TEAMS_WEBHOOK_URL="your-teams-url"
export GITHUB_WEBHOOK_SECRET="your-github-secret"

# Start services
docker-compose -f docker-compose.webhook.yml up -d
```

#### Using Kubernetes

```bash
# Create secrets
kubectl create secret generic tixl-webhook-secrets \
  --from-literal=slack-webhook="your-slack-url" \
  --from-literal=teams-webhook="your-teams-url" \
  --from-literal=github-secret="your-github-secret"

# Deploy
kubectl apply -f k8s/webhook-deployment.yml
```

### 3. Configure GitHub Webhooks

Run the setup script:

```bash
./scripts/setup-webhooks.sh REPO_OWNER REPO_NAME
```

This will create the necessary webhooks in your GitHub repository.

### 4. Test the Integration

```bash
# Test webhook server
curl http://localhost:5000/health

# Test notification channels
./scripts/test-webhooks.sh
```

## Notification Channels

### Slack

- **Channel**: #security-alerts
- **Triggers**: All security alerts
- **Template**: Rich formatting with action buttons

### Microsoft Teams

- **Channel**: Security team channel
- **Triggers**: Critical and high severity alerts
- **Template**: Adaptive cards with detailed information

### Email

- **Recipients**: security-team@company.com
- **Triggers**: Critical alerts and daily digests
- **Template**: HTML email with embedded details

### Discord

- **Channel**: Security alerts channel
- **Triggers**: All alerts (optional)
- **Template**: Simple embed formatting

## Monitoring

### Health Checks

- **Endpoint**: `GET /health`
- **Frequency**: Every 30 seconds
- **Timeout**: 5 seconds

### Metrics

- Request count and duration
- Notification success rates
- Error rates by channel
- Webhook processing latency

### Alerting

- Webhook delivery failures
- High processing latency
- Elevated error rates
- Channel notification failures

## Troubleshooting

### Common Issues

1. **Webhook not delivering**
   - Check webhook server logs
   - Verify webhook URLs are correct
   - Test network connectivity

2. **Notifications not appearing**
   - Verify webhook secrets match
   - Check channel permissions
   - Test webhook URLs manually

3. **High latency**
   - Monitor server resources
   - Check network performance
   - Review notification channel health

### Debug Commands

```bash
# Check webhook server status
curl -v http://localhost:5000/health

# Test GitHub webhook delivery
curl -X POST http://localhost:5000/webhook \
  -H "X-GitHub-Event: secret_scanning_alert" \
  -H "X-Hub-Signature-256: sha256=test" \
  -H "Content-Type: application/json" \
  -d '{"repository": {"full_name": "test/repo"}}'

# Monitor webhook logs
docker logs -f tixl-webhook-server
```

## Security Considerations

1. **Webhook Secrets**: Always use webhook secrets to verify authenticity
2. **HTTPS Only**: Ensure webhook URLs use HTTPS
3. **Access Control**: Restrict webhook server access to authorized networks
4. **Monitoring**: Enable comprehensive logging and monitoring
5. **Updates**: Regularly update webhook server dependencies

## Support

For issues with webhook integration:
1. Check the troubleshooting section
2. Review server logs
3. Contact the security team
4. Create an issue with the "webhook" label

---

**Last Updated**: 2025-11-02  
**Version**: 1.0
EOF

    print_status "Webhook documentation generated"
}

# Main execution
main() {
    echo "🔗 TiXL Secret Scanning Webhook Setup"
    echo "===================================="
    echo ""
    
    check_prerequisites
    echo ""
    
    create_webhook_config
    echo ""
    
    setup_github_webhooks
    echo ""
    
    create_webhook_server
    echo ""
    
    create_deployment_config
    echo ""
    
    create_monitoring_config
    echo ""
    
    generate_documentation
    echo ""
    
    echo "🎉 Webhook integration setup completed!"
    echo ""
    echo "Next steps:"
    echo "1. Update webhook URLs in .github/webhooks/webhook-config.json"
    echo "2. Deploy the webhook server using one of the provided configurations"
    echo "3. Test the integration with ./scripts/test-webhooks.sh"
    echo "4. Monitor webhook delivery and notification channels"
    echo ""
    echo "Documentation: .github/webhooks/README.md"
    echo "Configuration: .github/webhooks/webhook-config.json"
    echo "Server Script: scripts/webhook-server.py"
}

# Run main function
main "$@"