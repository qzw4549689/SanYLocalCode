# D365MetadataTool

## Purpose
A metadata management tool for Dynamics 365 (D365).

## Overview
This tool is used to extract, manage, and work with D365 entity metadata, including entity definitions, attributes, forms, views, plugin steps, and web resources.

## Project Structure

```
MetadataTool/
├── Program.cs                      # CLI entry point
├── D365MetadataTool.csproj         # Project file
├── README.md                       # This file
├── Helpers/
│   └── LabelHelper.cs              # Multi-language label helpers
├── Models/
│   └── EntityDefinition.cs         # Entity/field definition models
├── Services/
│   ├── EntityManager.cs            # Core entity/attribute/form/view operations
│   ├── QueryPluginSteps.cs         # Plugin step queries
│   ├── PublishProfileWebResources.cs # Profile web resource publisher
│   └── TestCommonService.cs        # D365ToolCommon shared library smoke tests
├── Scripts/
│   ├── register-bpp-plugins.ps1    # Register BPP plugins
│   └── register-credit-plugins.ps1 # Register all credit plugins
├── Plugins/                        # Plugin-related assets
├── WebResources/                   # Web resource assets
└── examples/                       # Usage examples (entity JSON definitions)
```

## Shared Library

This tool references `../D365ToolCommon/`, which provides reusable services for:

- D365 connection/authentication (`D365ConnectionFactory`)
- Plugin registration/query/deletion (`PluginRegistrationService`, `PluginQueryService`, `PluginStepDeletionService`)
- Web resource deployment (`WebResourceService`)
- Metadata field operations (`MetadataFieldService`)
- Publishing (`PublishingService`)

> When adding new capabilities, extend `D365ToolCommon` first instead of duplicating code in this tool.

## Technology
- .NET 10 / C#
- `Microsoft.PowerPlatform.Dataverse.Client`

## Environment

Target environment is controlled by the `D365_URL` environment variable. Defaults to DEV1 if not set.

Authentication priority:
1. `D365_CLIENTSECRET` — ClientSecret
2. `D365_USERNAME` + `D365_PASSWORD` — OAuth
3. Cached Device Code token

## Common Commands

```bash
# Create entity and fields from JSON definition
dotnet run create <entity-definition.json>

# Publish all or a specific entity
dotnet run publish [entityLogicalName]

# Query plugin steps by class name
dotnet run query-plugin-steps <className>

# Query plugin steps by namespace prefix
dotnet run query-plugin-namespace <namespacePrefix>

# Register a plugin with update filter
dotnet run register-plugin-update <dllPath> <className> [entity] [filteringAttributes]

# Register a plugin step with full config
dotnet run register-plugin-advanced <dllPath> <className> <entity> <message> <stage> [filter]

# Smoke test D365ToolCommon shared library (creates/deletes isolated test entity)
dotnet run test-common
```

## BPP / Credit Record Diagnostic Commands

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
```
