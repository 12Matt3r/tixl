# TiXL Tutorial Series: Intermediate Level

## Tutorial 1: Advanced Data Preparation and Transformation

### Learning Objectives
After completing this tutorial, you will be able to:
- Perform complex data transformations
- Handle multiple data sources and merge datasets
- Create custom data cleaning workflows
- Implement data validation and quality checks

### Prerequisites
- Completion of TiXL Beginner level or equivalent experience
- Familiarity with basic data operations and chart creation
- Understanding of data types and structures

### Duration: 45 minutes

### Tutorial Content

#### Part 1: Complex Data Transformations (15 minutes)

**Scenario**: You're working with a sales dataset that needs significant cleaning and transformation before analysis.

**Dataset**: sales_data_advanced.csv
- Contains sales transactions from multiple regions
- Missing values in several columns
- Inconsistent date formats
- Currency values in different formats

**Step 1: Advanced Import Techniques**
```
1. Import the sales dataset using the advanced import wizard
2. Select "Detect Data Types" to identify column types
3. Configure date parsing for multiple date formats
4. Set up custom parsing rules for currency values
5. Preview the import and make adjustments as needed
```

**Key Techniques Demonstrated:**
- Multi-format date parsing
- Currency normalization
- Data type detection and override
- Preview-based import configuration

**Step 2: Data Profiling and Analysis**
```
1. Generate a data profile report
2. Identify columns with missing values
3. Analyze distribution of numerical columns
4. Check for outliers and anomalies
5. Document data quality issues
```

**Key Insights:**
- 15% missing values in customer segment
- Date range spans 3 years
- Currency values need standardization
- Multiple product categories need grouping

#### Part 2: Handling Multiple Data Sources (15 minutes)

**Scenario**: Combine sales data with customer information and product details for comprehensive analysis.

**Additional Datasets**:
- customer_data.csv (customer demographics)
- product_data.csv (product categories and pricing)

**Step 1: Import Customer Data**
```
1. Import customer_data.csv
2. Map customer ID to sales data
3. Handle missing customer records
4. Validate customer data quality
5. Prepare for merging operations
```

**Step 2: Import Product Data**
```
1. Import product_data.csv
2. Map product IDs to sales transactions
3. Handle discontinued products
4. Add product category information
5. Validate pricing data consistency
```

**Step 3: Data Merging and Integration**
```
1. Use VLOOKUP/XLOOKUP to match customer data
2. Merge product information with sales records
3. Handle unmatched records and orphaned data
4. Create master dataset with all information
5. Validate merge quality and completeness
```

**Advanced Techniques:**
- Fuzzy matching for customer names
- Data deduplication strategies
- Handling many-to-many relationships
- Performance optimization for large datasets

#### Part 3: Custom Data Cleaning Workflows (15 minutes)

**Scenario**: Create reusable data cleaning workflows for consistent data preparation.

**Step 1: Create Data Cleaning Functions**
```
1. Build custom functions for common cleaning tasks
2. Handle missing values (imputation strategies)
3. Standardize text fields (case, spelling, format)
4. Validate data ranges and business rules
5. Create cleaning pipeline
```

**Functions to Build:**
- `clean_customer_names()` - Standardize customer name formatting
- `validate_dates()` - Check date ranges and formats
- `standardize_categories()` - Group similar product categories
- `calculate_totals()` - Verify calculation accuracy

**Step 2: Implement Quality Checks**
```
1. Create data quality rules
2. Build automated validation checks
3. Generate quality score for each dataset
4. Flag records that need review
5. Create quality monitoring dashboard
```

**Quality Metrics:**
- Completeness (percentage of non-null values)
- Accuracy (validation against business rules)
- Consistency (uniform formatting and standards)
- Timeliness (data freshness and currency)

**Step 3: Create Reusable Workflows**
```
1. Package cleaning functions into workflows
2. Add configurable parameters
3. Create logging and error handling
4. Build progress tracking and reporting
5. Save workflows for future use
```

### Practice Exercise: Sales Analysis Workflow

**Challenge**: Apply the learned techniques to create a comprehensive sales analysis workflow.

**Tasks**:
1. Import and merge all three datasets
2. Clean and standardize the combined data
3. Implement quality checks and validation
4. Create summary statistics and insights
5. Generate a data quality report

**Success Criteria**:
- Successfully merge datasets with >95% match rate
- Clean data to meet quality standards
- Generate meaningful insights from clean data
- Document the workflow for future use

### Quiz Questions

1. **Multiple Choice**: What is the best approach for handling missing customer segment data?
   a) Delete all records with missing segments
   b) Fill with most common segment value
   c) Use predictive modeling to estimate segments
   d) Mark as "Unknown" category

2. **Practical Application**: When merging customer and sales data, what should you do with customers who made no purchases?
   a) Exclude them from analysis
   b) Include them with zero sales values
   c) Use only customers with purchases
   d) Create dummy sales records

3. **Case Study**: Your dataset has 1,000 records, but merging with customer data only matches 850. What actions should you take?
   a) Accept 85% match rate as acceptable
   b) Investigate unmatched records and improve matching
   c) Delete unmatched sales records
   d) Fill missing customer data with averages

### Next Steps

- **Tutorial 2**: Advanced Statistical Analysis and Modeling
- **Advanced Project**: Build a comprehensive data cleaning workflow for your own dataset
- **Certification**: Apply knowledge to TiXL Certified Analyst preparation

### Resources

**Documentation**:
- TiXL Advanced Data Operations Guide
- Data Quality Framework Documentation
- Workflow Creation Tutorial

**Practice Datasets**:
- customer_satisfaction_survey.csv
- financial_transactions.csv
- marketing_campaign_data.csv

**Community Resources**:
- TiXL Data Preparation Forum
- Best Practices Library
- Expert Q&A Sessions

---

## Tutorial 2: Advanced Statistical Analysis and Modeling

### Learning Objectives
After completing this tutorial, you will be able to:
- Perform correlation and regression analysis
- Conduct hypothesis testing
- Create predictive models
- Interpret statistical results in business context

### Prerequisites
- Completion of Tutorial 1 or equivalent data preparation experience
- Basic understanding of statistical concepts
- Familiarity with TiXL intermediate features

### Duration: 60 minutes

### Tutorial Content

#### Part 1: Correlation Analysis (20 minutes)

**Scenario**: Analyze relationships between marketing spend, customer acquisition, and revenue.

**Dataset**: marketing_analysis.csv
- Marketing spend by channel
- Customer acquisition data
- Revenue and conversion metrics
- Time series data over 24 months

**Step 1: Correlation Matrix Creation**
```
1. Select numerical variables for analysis
2. Create correlation matrix using correlation function
3. Visualize correlations with heat map
4. Identify strong positive and negative correlations
5. Calculate confidence intervals for correlations
```

**Key Insights**:
- Strong positive correlation between digital spend and customer acquisition (r = 0.78)
- Negative correlation between traditional media spend and digital conversion
- Moderate correlation between customer acquisition and revenue (r = 0.65)

**Step 2: Scatter Plot Analysis**
```
1. Create scatter plots for key variable pairs
2. Add trend lines and R-squared values
3. Identify outliers and influential points
4. Segment analysis by time periods or categories
5. Create interactive scatter plot dashboard
```

**Advanced Techniques**:
- Bubble charts with third variable
- Color coding by category
- Interactive filtering and drilling
- Statistical significance testing

#### Part 2: Regression Analysis (20 minutes)

**Scenario**: Build predictive model for revenue based on marketing inputs.

**Step 1: Simple Linear Regression**
```
1. Set revenue as dependent variable
2. Select marketing spend as independent variable
3. Run regression analysis
4. Interpret coefficient, R-squared, and p-values
5. Create prediction interval bands
```

**Regression Output Interpretation**:
- Slope: For every $1 increase in marketing spend, revenue increases by $4.50
- R-squared: 61% of revenue variance explained by marketing spend
- P-value: 0.002 (highly significant relationship)

**Step 2: Multiple Regression Analysis**
```
1. Add multiple marketing channels as predictors
2. Check for multicollinearity using VIF
3. Perform stepwise regression to select best variables
4. Validate model assumptions (residuals, normality)
5. Compare model performance metrics
```

**Model Comparison**:
- Model 1 (Digital only): R² = 0.61, RMSE = 125,000
- Model 2 (All channels): R² = 0.78, RMSE = 95,000
- Model 3 (Optimized): R² = 0.81, RMSE = 88,000

#### Part 3: Hypothesis Testing (20 minutes)

**Scenario**: Test whether different marketing strategies have significantly different performance.

**Step 1: Two-Sample T-Test**
```
1. Compare performance between two marketing channels
2. Set up null and alternative hypotheses
3. Calculate test statistic and p-value
4. Interpret results at 95% confidence level
5. Create visualization of test results
```

**Hypothesis Test**:
- H₀: Digital and traditional marketing have equal performance
- H₁: Digital marketing performs differently than traditional
- T-statistic: 3.45, p-value: 0.001
- Conclusion: Reject H₀, digital marketing significantly outperforms

**Step 2: ANOVA Analysis**
```
1. Compare performance across multiple marketing channels
2. Set up ANOVA test structure
3. Calculate F-statistic and significance
4. Perform post-hoc tests for pairwise comparisons
5. Create visualization of group differences
```

**ANOVA Results**:
- F-statistic: 8.23, p-value: <0.001
- Significant differences between channels
- Post-hoc tests show digital > social > traditional

### Practice Exercise: Customer Segmentation Analysis

**Challenge**: Use statistical analysis to identify customer segments and their characteristics.

**Tasks**:
1. Analyze customer purchase patterns
2. Identify distinct customer segments
3. Profile each segment statistically
4. Create predictive model for segment membership
5. Develop targeted marketing recommendations

**Success Criteria**:
- Identify 3-5 distinct customer segments
- Statistically validate segment differences
- Create actionable segment profiles
- Build predictive model with >80% accuracy

### Advanced Applications

#### Time Series Analysis
- Trend decomposition and forecasting
- Seasonal pattern identification
- Moving averages and exponential smoothing
- ARIMA modeling basics

#### Classification Analysis
- Logistic regression for binary outcomes
- Decision tree analysis
- Model validation and cross-validation
- Performance metrics interpretation

### Quiz Questions

1. **Interpretation**: Your regression analysis shows R² = 0.75. What does this mean?
   a) 75% of observations fall on the regression line
   b) 75% of variance in Y is explained by X
   c) The correlation coefficient is 0.75
   d) 75% chance of accurate predictions

2. **Statistical Significance**: You get a p-value of 0.15 in your hypothesis test. At α = 0.05, what should you conclude?
   a) Reject the null hypothesis
   b) Fail to reject the null hypothesis
   c) The relationship is not important
   d) The sample size is too small

3. **Model Validation**: When validating a regression model, what should you look for in residual plots?
   a) Random scatter around zero
   b) Clear patterns or trends
   c) Funnel-shaped patterns
   d) All of the above except random scatter

### Next Steps

- **Tutorial 3**: Advanced Visualization and Dashboard Design
- **Capstone Project**: Complete statistical analysis of real business dataset
- **Certification Path**: Prepare for TiXL Certified Expert level

### Additional Resources

**Statistical Reference**:
- Business Statistics Guide
- Regression Analysis Handbook
- Hypothesis Testing Quick Reference

**Practice Datasets**:
- retail_sales_analysis.csv
- customer_behavior_study.csv
- competitive_analysis_data.csv

**Community Features**:
- Statistical Analysis Forum
- Model Validation Templates
- Expert Consultation Sessions

---

*Continue to Tutorial 3 for Advanced Visualization and Dashboard Design*