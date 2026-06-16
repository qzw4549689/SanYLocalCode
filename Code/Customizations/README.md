# Customizations

## Purpose
All D365 custom development assets - plugins, web resources, entities, and workflows.

## Structure

### Plugins/
C# plugins for D365 business logic:
- `Account/` - Account validation plugins
- `BppIntegration/` - BPP integration plugins
- `CofaceIntegration/` - Coface API integration plugins
- `CreditItemValue/` - Credit item value validation plugins
- `CreditItems/` - Credit items plugins
- `CreditRecord/` - Credit record auto-number plugins
- `CreditScore/` - Credit score calculation plugins
- `CustomerTag/` - Customer tag initialization plugins
- `ScoringCard/` - Scoring card auto-number plugins

### WebResources/
Client-side customizations:
- `JS/` - JavaScript web resources for form scripts
  - `mcs_account.js`
  - `mcs_credit_items.js`
  - `mcs_credit_record.js`
  - `mcs_credit_scoringcard.js`
  - `mcs_credititem_value.js`
  - `mcs_customer_tag.js`
- `HTML/` - HTML web resources (if any)

### Entities/
Custom entity definitions exported from D365.

### Workflows/
Custom workflow assemblies (if any).
