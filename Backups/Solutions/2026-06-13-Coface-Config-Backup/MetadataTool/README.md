# D365MetadataTool

## Purpose
A metadata management tool for Dynamics 365 (D365).

## Overview
This tool is used to extract, manage, and work with D365 entity metadata, including entity definitions, attributes, and relationships.

## Key Components
- `EntityDefinition.cs` - Entity definition models
- `Plugins/` - Plugin-related utilities
- `WebResources/` - Web resource management
- `examples/` - Usage examples

## Technology
- .NET / C#

## BPP / Credit Record Diagnostic Commands

The following commands were added to help diagnose BPP integration issues (e.g. credit record status 14 submission with no workflowid).

Target environment is controlled by the `D365_URL` environment variable. Defaults to DEV1 if not set.

```bash
# Query recent mcs_credit_record rows (optionally filter by status)
D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- query-credit-records [14]

# Query recent mcs_bppapply rows
D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- query-bppapply

# Query message-queue configuration ms_squeue
D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- query-squeue [groupName]

# Query message entities ms_smessage_common_* (type=BPPStartWorkflow, BPPApprovalMessage, Transfer, all, ...)
D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- query-smessage [type|all]

# Find messages whose JSON data contains a given entity id
D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- find-smessage-by-entity <entityId>

# List registered plugin assemblies (optional name filter)
D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- list-assemblies [filter]

# Query plugin steps for a given class name
D365_URL=https://sany-uat.crm5.dynamics.com dotnet run -- query-plugin-steps <className>
```
