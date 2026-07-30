# Tools

## Purpose
Development and deployment tools for the D365 project.

## 元数据创建红线

**所有实体、字段、表单、视图、关系、WebResource 等元数据的创建与更新，必须使用 `D365ToolCommon` 或 `MetadataTool` 中已有的公共方法。**

- **严禁**在任意工具、脚本、插件中直接调用 `CreateAttributeRequest`、`CreateEntityRequest`、`UpdateEntityRequest` 等 SDK 原生 API 创建元数据
- **严禁**临时编写新的元数据创建方法
- 如现有公共方法不存在或不能满足需求，**必须向负责人提出申请，获批准后方可修改或扩展公共方法**

## Structure

### MetadataTool/
A .NET console application for extracting and managing D365 entity metadata.
- Extract entity definitions
- Manage metadata exports
- Generate deployment scripts

### SolutionViewer/
A Node.js web application for browsing D365 solution components.
- Visual solution explorer
- Component metadata viewer
