# SanyD365Project

## Purpose
The main Sany D365 project - solution packaging, deployment, and core configurations.

## Overview
This project contains the D365 solution package, deployment configurations, and project documentation.

## Structure

### config/
Deployment configuration files.

### scripts/
Build and deployment scripts.

### solutions/
D365 solution packages.

### src/
Source code and customizations.

### Tests/
Automated testing assets (Playwright E2E tests).
- `e2e/` - End-to-end test cases
- `playwright/` - Playwright configuration
- `playwright-report/` - Test reports
- `playwright.config.ts` - Playwright config
- `package.json` - Node dependencies for testing

### Backups/
Backup files and archives.

### Exports/
D365 entity exports and CDS project files.
- `entity_20260603_peter/` - Exported entity folder
- `entity_20260603_peter.cdsproj` - CDS project file
- `entity_20260603_peter.zip` - Exported solution zip
- `export.log` - Export log file

## Technology
- Dynamics 365 / Power Platform
- Playwright (E2E testing)
