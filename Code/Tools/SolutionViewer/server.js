const express = require('express');
const fs = require('fs');
const path = require('path');
const xml2js = require('xml2js');

const app = express();
const PORT = 3456;

// 解析 Solution 路径（从环境变量或默认路径）
const SOLUTION_PATH = process.env.SOLUTION_PATH || path.join(__dirname, '..', 'SanyD365Project', 'src');
const ZIP_PATH = process.env.ZIP_PATH || path.join(__dirname, '..', 'SanyD365Project', 'entity_20260603_peter.zip');

// 缓存解析后的数据
let cachedEntities = null;
let cachedFields = {};
let cachedOptions = {};

app.use(express.static('public'));

// ========== 数据加载 ==========

async function loadAllData() {
    if (cachedEntities) return;
    
    cachedEntities = [];
    cachedFields = {};
    cachedOptions = {};
    
    // 方式1: 从 ZIP 文件加载（包含所有实体）
    if (fs.existsSync(ZIP_PATH)) {
        console.log(`Loading from ZIP: ${ZIP_PATH}`);
        await loadFromZip(ZIP_PATH);
    }
    
    // 方式2: 从本地 src/Entities/ 加载（补充）
    const entitiesDir = path.join(SOLUTION_PATH, 'Entities');
    if (fs.existsSync(entitiesDir)) {
        console.log(`Loading from local: ${entitiesDir}`);
        await loadFromLocal(entitiesDir);
    }
    
    console.log(`Loaded ${cachedEntities.length} entities`);
}

async function loadFromZip(zipPath) {
    const AdmZip = require('adm-zip');
    const zip = new AdmZip(zipPath);
    
    const customizationsEntry = zip.getEntry('customizations.xml');
    if (!customizationsEntry) return;
    
    const xml = zip.readAsText(customizationsEntry);
    const parser = new xml2js.Parser({ explicitArray: false });
    const result = await parser.parseStringPromise(xml);
    
    const entities = result.ImportExportXml?.Entities?.Entity;
    if (!entities) return;
    
    const entityList = Array.isArray(entities) ? entities : [entities];
    
    // 解析 EntityRelationships 获取 Lookup 关联关系
    const lookupTargets = parseEntityRelationships(result.ImportExportXml?.EntityRelationships?.EntityRelationship);
    
    for (const entity of entityList) {
        const logicalName = entity.Name?._ || entity.Name;
        if (!logicalName) continue;
        
        // 检查是否已存在
        if (cachedEntities.find(e => e.logicalName === logicalName)) continue;
        
        const entityInfo = await parseEntityFromZip(entity);
        cachedEntities.push({
            logicalName,
            ...entityInfo
        });
        
        // 解析字段
        const fields = parseFieldsFromZip(entity, lookupTargets[logicalName] || {});
        cachedFields[logicalName] = fields;
        
        // 解析选项
        for (const field of fields) {
            if (field.hasOptions && field.options) {
                cachedOptions[`${logicalName}.${field.logicalName}`] = field.options;
            }
        }
    }
}

// 解析 EntityRelationships 获取 Lookup 字段关联的目标实体
function parseEntityRelationships(relationships) {
    const lookupMap = {}; // { entityName: { fieldName: targetEntity } }
    
    if (!relationships) return lookupMap;
    
    const relList = Array.isArray(relationships) ? relationships : [relationships];
    
    for (const rel of relList) {
        const relType = rel.EntityRelationshipType;
        if (relType !== 'OneToMany') continue;
        
        const referencingEntity = rel.ReferencingEntityName;
        const referencingAttribute = rel.ReferencingAttributeName;
        const referencedEntity = rel.ReferencedEntityName;
        
        if (referencingEntity && referencingAttribute && referencedEntity) {
            if (!lookupMap[referencingEntity]) {
                lookupMap[referencingEntity] = {};
            }
            lookupMap[referencingEntity][referencingAttribute] = referencedEntity;
        }
    }
    
    return lookupMap;
}

async function loadFromLocal(entitiesDir) {
    const entityFolders = fs.readdirSync(entitiesDir).filter(f => {
        return fs.statSync(path.join(entitiesDir, f)).isDirectory();
    });
    
    for (const folder of entityFolders) {
        // 检查是否已存在
        if (cachedEntities.find(e => e.logicalName === folder)) continue;
        
        const entityXmlPath = path.join(entitiesDir, folder, 'Entity.xml');
        if (fs.existsSync(entityXmlPath)) {
            const entityInfo = await parseEntityXml(entityXmlPath);
            cachedEntities.push({
                logicalName: folder,
                ...entityInfo
            });
            
            // 解析字段
            const fields = await parseEntityFields(entityXmlPath);
            cachedFields[folder] = fields;
            
            // 解析选项
            for (const field of fields) {
                if (field.hasOptions) {
                    const options = await parseFieldOptions(entityXmlPath, field.logicalName);
                    cachedOptions[`${folder}.${field.logicalName}`] = options;
                }
            }
        }
    }
}

// ========== ZIP 解析 ==========

async function parseEntityFromZip(entity) {
    const entityInfo = entity.EntityInfo?.entity || {};
    
    let displayName = '';
    const localizedNames = entityInfo.LocalizedNames?.LocalizedName;
    if (localizedNames) {
        if (Array.isArray(localizedNames)) {
            const cnName = localizedNames.find(n => n.$?.languagecode === '2052');
            displayName = cnName?.$.description || localizedNames[0]?.$.description || '';
        } else {
            displayName = localizedNames.$?.description || '';
        }
    }
    
    const schemaName = entity.Name?._ || entityInfo.$?.Name || '';
    
    return {
        displayName,
        schemaName,
        description: displayName
    };
}

function parseFieldsFromZip(entity, lookupTargets = {}) {
    const attributes = entity.EntityInfo?.entity?.attributes?.attribute;
    if (!attributes) return [];
    
    const attrList = Array.isArray(attributes) ? attributes : [attributes];
    
    return attrList.map(attr => {
        const logicalName = attr.LogicalName || attr.Name || '';
        const schemaName = attr.PhysicalName || attr.Name || '';
        const type = attr.Type || 'unknown';
        
        let displayName = '';
        const displaynames = attr.displaynames?.displayname;
        if (displaynames) {
            if (Array.isArray(displaynames)) {
                // 优先取中文(2052)，但如果中文是逻辑名本身，则尝试取英文(1033)
                const cnName = displaynames.find(n => n.$?.languagecode === '2052');
                const enName = displaynames.find(n => n.$?.languagecode === '1033');
                const firstName = displaynames[0];
                
                let candidate = cnName?.$.description || '';
                // 如果中文显示名就是逻辑名本身（没有实际翻译），优先用英文
                if (candidate === logicalName && enName && enName.$.description !== logicalName) {
                    candidate = enName.$.description;
                }
                displayName = candidate || firstName?.$.description || '';
            } else {
                displayName = displaynames.$?.description || '';
            }
        }
        
        // 解析选项值 - 支持 ZIP 导出格式（小写标签）
        let options = null;
        const optionSet = attr.OptionSet || attr.optionset;
        const hasOptions = type === 'picklist' || type === 'boolean' || optionSet;
        
        if (hasOptions && optionSet) {
            options = [];
            
            // Picklist options - 支持两种格式
            const optionsContainer = optionSet.Options || optionSet.options;
            if (optionsContainer) {
                const optionItems = optionsContainer.Option || optionsContainer.option;
                if (optionItems) {
                    const opts = Array.isArray(optionItems) ? optionItems : [optionItems];
                    
                    opts.forEach(opt => {
                        const value = opt.$?.value || opt.Value || '';
                        let label = '';
                        
                        // 支持 Labels/Label 和 labels/label 两种格式
                        const labelsContainer = opt.Labels || opt.labels;
                        if (labelsContainer) {
                            const labelItems = labelsContainer.Label || labelsContainer.label;
                            if (labelItems) {
                                if (Array.isArray(labelItems)) {
                                    const cnLabel = labelItems.find(l => l.$?.languagecode === '2052');
                                    label = cnLabel?.$.description || labelItems[0]?.$.description || '';
                                } else {
                                    label = labelItems.$?.description || '';
                                }
                            }
                        }
                        options.push({ value, label });
                    });
                }
            }
            
            // Boolean options
            if (optionSet.TrueOption || optionSet.FalseOption) {
                const trueOpt = optionSet.TrueOption;
                const falseOpt = optionSet.FalseOption;
                
                if (trueOpt) {
                    let trueLabel = '';
                    const labels = trueOpt.Labels?.Label;
                    if (labels) {
                        trueLabel = Array.isArray(labels) 
                            ? (labels.find(l => l.$?.languagecode === '2052')?.$.description || labels[0]?.$.description)
                            : labels.$?.description;
                    }
                    options.push({ value: '1', label: trueLabel || '是' });
                }
                
                if (falseOpt) {
                    let falseLabel = '';
                    const labels = falseOpt.Labels?.Label;
                    if (labels) {
                        falseLabel = Array.isArray(labels)
                            ? (labels.find(l => l.$?.languagecode === '2052')?.$.description || labels[0]?.$.description)
                            : labels.$?.description;
                    }
                    options.push({ value: '0', label: falseLabel || '否' });
                }
            }
        }
        
        // 解析 Lookup 字段关联的目标实体（从 EntityRelationships）
        let lookupTarget = lookupTargets[logicalName] || null;
        
        return {
            logicalName,
            schemaName,
            displayName,
            type,
            hasOptions: !!hasOptions,
            options,
            lookupTarget,
            required: attr.RequiredLevel?._ === 'systemrequired' || attr.RequiredLevel?._ === 'required'
        };
    });
}

// ========== 本地 XML 解析 ==========

async function parseEntityXml(xmlPath) {
    const xml = fs.readFileSync(xmlPath, 'utf-8');
    const parser = new xml2js.Parser({ explicitArray: false });
    const result = await parser.parseStringPromise(xml);
    
    const entityInfo = result.Entity?.EntityInfo?.entity || {};
    
    let displayName = '';
    const localizedNames = entityInfo.LocalizedNames?.LocalizedName;
    if (localizedNames) {
        if (Array.isArray(localizedNames)) {
            const cnName = localizedNames.find(n => n.$?.languagecode === '2052');
            displayName = cnName?.$.description || localizedNames[0]?.$.description || '';
        } else {
            displayName = localizedNames.$?.description || '';
        }
    }
    
    const schemaName = result.Entity?.Name?._ || entityInfo.$?.Name || '';
    
    return {
        displayName,
        schemaName,
        description: displayName
    };
}

async function parseEntityFields(xmlPath) {
    const xml = fs.readFileSync(xmlPath, 'utf-8');
    const parser = new xml2js.Parser({ explicitArray: false });
    const result = await parser.parseStringPromise(xml);
    
    const attributes = result.Entity?.EntityInfo?.entity?.attributes?.attribute;
    if (!attributes) return [];
    
    const attrList = Array.isArray(attributes) ? attributes : [attributes];
    
    return attrList.map(attr => {
        const logicalName = attr.LogicalName || attr.Name || '';
        const schemaName = attr.PhysicalName || attr.Name || '';
        const type = attr.Type || 'unknown';
        
        let displayName = '';
        const displaynames = attr.displaynames?.displayname;
        if (displaynames) {
            if (Array.isArray(displaynames)) {
                const cnName = displaynames.find(n => n.$?.languagecode === '2052');
                displayName = cnName?.$.description || displaynames[0]?.$.description || '';
            } else {
                displayName = displaynames.$?.description || '';
            }
        }
        
        const hasOptions = type === 'picklist' || type === 'boolean' || attr.OptionSet || attr.OptionSetName;
        
        return {
            logicalName,
            schemaName,
            displayName,
            type,
            hasOptions: !!hasOptions,
            required: attr.RequiredLevel?._ === 'systemrequired' || attr.RequiredLevel?._ === 'required'
        };
    });
}

async function parseFieldOptions(xmlPath, fieldName) {
    const xml = fs.readFileSync(xmlPath, 'utf-8');
    const parser = new xml2js.Parser({ explicitArray: false });
    const result = await parser.parseStringPromise(xml);
    
    const attributes = result.Entity?.EntityInfo?.entity?.attributes?.attribute;
    if (!attributes) return [];
    
    const attrList = Array.isArray(attributes) ? attributes : [attributes];
    const field = attrList.find(a => (a.LogicalName || a.Name) === fieldName);
    
    if (!field) return [];
    
    const options = [];
    
    if (field.OptionSet?.Options?.Option) {
        const opts = Array.isArray(field.OptionSet.Options.Option) 
            ? field.OptionSet.Options.Option 
            : [field.OptionSet.Options.Option];
        
        opts.forEach(opt => {
            const value = opt.$?.value || opt.Value || '';
            let label = '';
            const labels = opt.Labels?.Label;
            if (labels) {
                if (Array.isArray(labels)) {
                    const cnLabel = labels.find(l => l.$?.languagecode === '2052');
                    label = cnLabel?.$.description || labels[0]?.$.description || '';
                } else {
                    label = labels.$?.description || '';
                }
            }
            options.push({ value, label });
        });
    }
    
    if (field.OptionSet?.TrueOption || field.OptionSet?.FalseOption) {
        const trueOpt = field.OptionSet.TrueOption;
        const falseOpt = field.OptionSet.FalseOption;
        
        if (trueOpt) {
            let trueLabel = '';
            const labels = trueOpt.Labels?.Label;
            if (labels) {
                trueLabel = Array.isArray(labels) 
                    ? (labels.find(l => l.$?.languagecode === '2052')?.$.description || labels[0]?.$.description)
                    : labels.$?.description;
            }
            options.push({ value: '1', label: trueLabel || '是' });
        }
        
        if (falseOpt) {
            let falseLabel = '';
            const labels = falseOpt.Labels?.Label;
            if (labels) {
                falseLabel = Array.isArray(labels)
                    ? (labels.find(l => l.$?.languagecode === '2052')?.$.description || labels[0]?.$.description)
                    : labels.$?.description;
            }
            options.push({ value: '0', label: falseLabel || '否' });
        }
    }
    
    return options;
}

// ========== API 路由 ==========

// API: 获取所有实体列表
app.get('/api/entities', async (req, res) => {
    try {
        await loadAllData();
        res.json({ entities: cachedEntities });
    } catch (err) {
        res.status(500).json({ error: err.message });
    }
});

// API: 获取指定实体的字段列表
app.get('/api/entities/:entityName/fields', async (req, res) => {
    try {
        await loadAllData();
        const { entityName } = req.params;
        const fields = cachedFields[entityName] || [];
        res.json({ entityName, fields });
    } catch (err) {
        res.status(500).json({ error: err.message });
    }
});

// API: 获取字段的选项值
app.get('/api/entities/:entityName/fields/:fieldName/options', async (req, res) => {
    try {
        await loadAllData();
        const { entityName, fieldName } = req.params;
        const options = cachedOptions[`${entityName}.${fieldName}`] || [];
        res.json({ entityName, fieldName, options });
    } catch (err) {
        res.status(500).json({ error: err.message });
    }
});

// 启动服务器
app.listen(PORT, () => {
    console.log(`Solution Viewer running at http://localhost:${PORT}`);
    console.log(`Solution path: ${SOLUTION_PATH}`);
    console.log(`ZIP path: ${ZIP_PATH}`);
});
