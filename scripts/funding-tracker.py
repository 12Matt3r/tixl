#!/usr/bin/env python3
"""
TiXL Funding Tracker
A comprehensive tool for tracking revenue streams, expenses, and financial projections.
"""

import json
import csv
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass, asdict
from collections import defaultdict
import pandas as pd
import matplotlib.pyplot as plt
import seaborn as sns
from pathlib import Path

@dataclass
class RevenueEntry:
    """Data class for revenue entries"""
    date: str
    source: str  # sponsorship, training, consulting, etc.
    amount: float
    category: str  # recurring, one-time, milestone
    client: str
    description: str = ""
    status: str = "pending"  # pending, confirmed, received

@dataclass
class ExpenseEntry:
    """Data class for expense entries"""
    date: str
    category: str
    amount: float
    description: str
    vendor: str
    necessary: bool = True
    project: str = ""

class FundingTracker:
    """Main class for tracking funding and expenses"""
    
    def __init__(self, data_file: str = "funding_data.json"):
        self.data_file = Path(data_file)
        self.revenue_data: List[RevenueEntry] = []
        self.expense_data: List[ExpenseEntry] = []
        self.projections: Dict[str, List[Dict]] = {}
        self.load_data()
    
    def load_data(self):
        """Load data from JSON file"""
        if self.data_file.exists():
            with open(self.data_file, 'r') as f:
                data = json.load(f)
                self.revenue_data = [RevenueEntry(**entry) for entry in data.get('revenue', [])]
                self.expense_data = [ExpenseEntry(**entry) for entry in data.get('expenses', [])]
                self.projections = data.get('projections', {})
    
    def save_data(self):
        """Save data to JSON file"""
        data = {
            'revenue': [asdict(entry) for entry in self.revenue_data],
            'expenses': [asdict(entry) for entry in self.expense_data],
            'projections': self.projections
        }
        with open(self.data_file, 'w') as f:
            json.dump(data, f, indent=2)
    
    def add_revenue(self, entry: RevenueEntry):
        """Add a new revenue entry"""
        self.revenue_data.append(entry)
        self.save_data()
        print(f"Added revenue: ${entry.amount:,.2f} from {entry.source}")
    
    def add_expense(self, entry: ExpenseEntry):
        """Add a new expense entry"""
        self.expense_data.append(entry)
        self.save_data()
        print(f"Added expense: ${entry.amount:,.2f} for {entry.category}")
    
    def get_monthly_summary(self, year: int, month: int) -> Dict:
        """Get monthly summary of revenue and expenses"""
        # Filter revenue for the month
        monthly_revenue = [
            entry for entry in self.revenue_data
            if datetime.strptime(entry.date, '%Y-%m-%d').year == year
            and datetime.strptime(entry.date, '%Y-%m-%d').month == month
        ]
        
        # Filter expenses for the month
        monthly_expenses = [
            entry for entry in self.expense_data
            if datetime.strptime(entry.date, '%Y-%m-%d').year == year
            and datetime.strptime(entry.date, '%Y-%m-%d').month == month
        ]
        
        # Calculate totals
        revenue_by_source = defaultdict(float)
        for entry in monthly_revenue:
            revenue_by_source[entry.source] += entry.amount
        
        expense_by_category = defaultdict(float)
        for entry in monthly_expenses:
            expense_by_category[entry.category] += entry.amount
        
        return {
            'revenue': {
                'total': sum(entry.amount for entry in monthly_revenue),
                'by_source': dict(revenue_by_source),
                'count': len(monthly_revenue)
            },
            'expenses': {
                'total': sum(entry.amount for entry in monthly_expenses),
                'by_category': dict(expense_by_category),
                'count': len(monthly_expenses)
            },
            'net_income': sum(entry.amount for entry in monthly_revenue) - 
                         sum(entry.amount for entry in monthly_expenses)
        }
    
    def get_yearly_summary(self, year: int) -> Dict:
        """Get yearly summary"""
        # Get all months in the year
        monthly_data = {}
        for month in range(1, 13):
            monthly_data[month] = self.get_monthly_summary(year, month)
        
        total_revenue = sum(data['revenue']['total'] for data in monthly_data.values())
        total_expenses = sum(data['expenses']['total'] for data in monthly_data.values())
        
        return {
            'total_revenue': total_revenue,
            'total_expenses': total_expenses,
            'net_income': total_revenue - total_expenses,
            'monthly_breakdown': monthly_data
        }
    
    def add_projection(self, revenue_source: str, projection: Dict):
        """Add revenue projection for a specific source"""
        if revenue_source not in self.projections:
            self.projections[revenue_source] = []
        self.projections[revenue_source].append(projection)
        self.save_data()
    
    def get_projection_summary(self) -> Dict:
        """Get summary of all projections"""
        total_projected = {}
        for source, projections in self.projections.items():
            total_projected[source] = sum(p.get('amount', 0) for p in projections)
        
        return {
            'by_source': total_projected,
            'total_projected': sum(total_projected.values())
        }
    
    def generate_report(self, year: int = None) -> str:
        """Generate comprehensive financial report"""
        if year is None:
            year = datetime.now().year
        
        summary = self.get_yearly_summary(year)
        projections = self.get_projection_summary()
        
        report = f"""
TiXL Funding Tracker - Financial Report {year}
{'=' * 50}

REVENUE SUMMARY
Total Revenue: ${summary['total_revenue']:,.2f}

Revenue by Source:
"""
        
        # Calculate average monthly revenue by source
        source_totals = defaultdict(float)
        source_counts = defaultdict(int)
        
        for month_data in summary['monthly_breakdown'].values():
            for source, amount in month_data['revenue']['by_source'].items():
                source_totals[source] += amount
                source_counts[source] += 1
        
        for source, total in source_totals.items():
            avg_monthly = total / 12 if source_counts[source] > 0 else 0
            report += f"  {source}: ${total:,.2f} (${avg_monthly:,.2f}/month avg)\n"
        
        report += f"""
EXPENSE SUMMARY
Total Expenses: ${summary['total_expenses']:,.2f}

Expense by Category:
"""
        
        expense_totals = defaultdict(float)
        for month_data in summary['monthly_breakdown'].values():
            for category, amount in month_data['expenses']['by_category'].items():
                expense_totals[category] += amount
        
        for category, total in expense_totals.items():
            report += f"  {category}: ${total:,.2f}\n"
        
        report += f"""
NET INCOME: ${summary['net_income']:,.2f}
Profit Margin: {(summary['net_income'] / summary['total_revenue'] * 100):.1f}%

PROJECTIONS
Total Projected Revenue: ${projections['total_projected']:,.2f}

Projected Revenue by Source:
"""
        
        for source, amount in projections['by_source'].items():
            report += f"  {source}: ${amount:,.2f}\n"
        
        report += f"""
MONTHLY PERFORMANCE:
"""
        for month, data in summary['monthly_breakdown'].items():
            report += f"  Month {month:2d}: Revenue ${data['revenue']['total']:8,.2f} | "
            report += f"Expenses ${data['expenses']['total']:8,.2f} | "
            report += f"Net ${data['net_income']:8,.2f}\n"
        
        return report
    
    def export_to_csv(self, filename: str = None):
        """Export data to CSV files"""
        if filename is None:
            timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
            filename = f"funding_export_{timestamp}"
        
        # Export revenue data
        revenue_df = pd.DataFrame([asdict(entry) for entry in self.revenue_data])
        revenue_df.to_csv(f"{filename}_revenue.csv", index=False)
        
        # Export expense data
        expense_df = pd.DataFrame([asdict(entry) for entry in self.expense_data])
        expense_df.to_csv(f"{filename}_expenses.csv", index=False)
        
        print(f"Data exported to {filename}_revenue.csv and {filename}_expenses.csv")
    
    def create_visualizations(self, year: int = None):
        """Create financial visualizations"""
        if year is None:
            year = datetime.now().year
        
        summary = self.get_yearly_summary(year)
        
        # Create figure with subplots
        fig, ((ax1, ax2), (ax3, ax4)) = plt.subplots(2, 2, figsize=(15, 12))
        fig.suptitle(f'TiXL Financial Dashboard - {year}', fontsize=16, fontweight='bold')
        
        # 1. Monthly Revenue vs Expenses
        months = list(summary['monthly_breakdown'].keys())
        revenues = [summary['monthly_breakdown'][m]['revenue']['total'] for m in months]
        expenses = [summary['monthly_breakdown'][m]['expenses']['total'] for m in months]
        
        ax1.plot(months, revenues, marker='o', linewidth=2, label='Revenue', color='green')
        ax1.plot(months, expenses, marker='s', linewidth=2, label='Expenses', color='red')
        ax1.set_title('Monthly Revenue vs Expenses')
        ax1.set_xlabel('Month')
        ax1.set_ylabel('Amount ($)')
        ax1.legend()
        ax1.grid(True, alpha=0.3)
        
        # 2. Revenue by Source (Pie Chart)
        source_totals = defaultdict(float)
        for month_data in summary['monthly_breakdown'].values():
            for source, amount in month_data['revenue']['by_source'].items():
                source_totals[source] += amount
        
        ax2.pie(source_totals.values(), labels=source_totals.keys(), autopct='%1.1f%%')
        ax2.set_title('Revenue Distribution by Source')
        
        # 3. Monthly Net Income
        net_incomes = [summary['monthly_breakdown'][m]['net_income'] for m in months]
        colors = ['green' if x >= 0 else 'red' for x in net_incomes]
        ax3.bar(months, net_incomes, color=colors, alpha=0.7)
        ax3.set_title('Monthly Net Income')
        ax3.set_xlabel('Month')
        ax3.set_ylabel('Net Income ($)')
        ax3.axhline(y=0, color='black', linestyle='-', alpha=0.5)
        ax3.grid(True, alpha=0.3)
        
        # 4. Cumulative Performance
        cumulative_revenue = []
        cumulative_expenses = []
        cum_rev = 0
        cum_exp = 0
        
        for month in months:
            cum_rev += summary['monthly_breakdown'][month]['revenue']['total']
            cum_exp += summary['monthly_breakdown'][month]['expenses']['total']
            cumulative_revenue.append(cum_rev)
            cumulative_expenses.append(cum_exp)
        
        ax4.fill_between(months, cumulative_revenue, alpha=0.3, color='green', label='Cumulative Revenue')
        ax4.fill_between(months, cumulative_expenses, alpha=0.3, color='red', label='Cumulative Expenses')
        ax4.plot(months, cumulative_revenue, color='green', linewidth=2)
        ax4.plot(months, cumulative_expenses, color='red', linewidth=2)
        ax4.set_title('Cumulative Performance')
        ax4.set_xlabel('Month')
        ax4.set_ylabel('Cumulative Amount ($)')
        ax4.legend()
        ax4.grid(True, alpha=0.3)
        
        plt.tight_layout()
        
        # Save the plot
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        plt.savefig(f'financial_dashboard_{year}_{timestamp}.png', dpi=300, bbox_inches='tight')
        plt.show()
        
        print(f"Dashboard saved as financial_dashboard_{year}_{timestamp}.png")

def main():
    """Main function for interactive use"""
    tracker = FundingTracker()
    
    print("TiXL Funding Tracker")
    print("===================")
    print("1. Add Revenue Entry")
    print("2. Add Expense Entry")
    print("3. View Monthly Summary")
    print("4. View Yearly Summary")
    print("5. Add Revenue Projection")
    print("6. Generate Report")
    print("7. Export to CSV")
    print("8. Create Visualizations")
    print("9. Exit")
    
    while True:
        choice = input("\nSelect an option (1-9): ").strip()
        
        if choice == '1':
            # Add revenue
            date = input("Date (YYYY-MM-DD): ")
            source = input("Revenue source: ")
            amount = float(input("Amount: $"))
            category = input("Category (recurring/one-time/milestone): ")
            client = input("Client name: ")
            description = input("Description (optional): ")
            status = input("Status (pending/confirmed/received): ")
            
            entry = RevenueEntry(date, source, amount, category, client, description, status)
            tracker.add_revenue(entry)
        
        elif choice == '2':
            # Add expense
            date = input("Date (YYYY-MM-DD): ")
            category = input("Expense category: ")
            amount = float(input("Amount: $"))
            description = input("Description: ")
            vendor = input("Vendor: ")
            necessary = input("Necessary? (y/n): ").lower() == 'y'
            project = input("Project (optional): ")
            
            entry = ExpenseEntry(date, category, amount, description, vendor, necessary, project)
            tracker.add_expense(entry)
        
        elif choice == '3':
            # Monthly summary
            year = int(input("Year: "))
            month = int(input("Month: "))
            summary = tracker.get_monthly_summary(year, month)
            
            print(f"\nMonthly Summary - {year}-{month:02d}")
            print(f"Total Revenue: ${summary['revenue']['total']:,.2f}")
            print(f"Total Expenses: ${summary['expenses']['total']:,.2f}")
            print(f"Net Income: ${summary['net_income']:,.2f}")
        
        elif choice == '4':
            # Yearly summary
            year = int(input("Year: "))
            summary = tracker.get_yearly_summary(year)
            
            print(f"\nYearly Summary - {year}")
            print(f"Total Revenue: ${summary['total_revenue']:,.2f}")
            print(f"Total Expenses: ${summary['total_expenses']:,.2f}")
            print(f"Net Income: ${summary['net_income']:,.2f}")
        
        elif choice == '5':
            # Add projection
            source = input("Revenue source for projection: ")
            month = input("Projected month (YYYY-MM): ")
            amount = float(input("Projected amount: $"))
            confidence = input("Confidence level (low/medium/high): ")
            
            projection = {
                'month': month,
                'amount': amount,
                'confidence': confidence,
                'created_date': datetime.now().strftime('%Y-%m-%d')
            }
            
            tracker.add_projection(source, projection)
            print(f"Projection added for {source}")
        
        elif choice == '6':
            # Generate report
            year = int(input("Year for report (default current): ") or datetime.now().year)
            report = tracker.generate_report(year)
            print(report)
            
            # Save report to file
            with open(f"financial_report_{year}.txt", 'w') as f:
                f.write(report)
            print(f"Report saved to financial_report_{year}.txt")
        
        elif choice == '7':
            # Export to CSV
            tracker.export_to_csv()
        
        elif choice == '8':
            # Create visualizations
            year = int(input("Year for visualization (default current): ") or datetime.now().year)
            tracker.create_visualizations(year)
        
        elif choice == '9':
            print("Exiting TiXL Funding Tracker. Goodbye!")
            break
        
        else:
            print("Invalid option. Please select 1-9.")

if __name__ == "__main__":
    main()
