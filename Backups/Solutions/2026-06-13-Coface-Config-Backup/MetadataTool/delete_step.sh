#!/bin/bash
cd /Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Tools/MetadataTool

# 备份
cp Program.cs Program.cs.bak2

# 在 register-plugin-update case 前添加删除逻辑
sed -i '' '242a\
                    case "delete-plugin-step":\
                        if (args.Length < 3)\
                        {\
                            Console.WriteLine("用法: dotnet run delete-plugin-step <类名> <消息名>");\
                            return;\
                        }\
                        DeletePluginStep(manager, args[1], args[2]);\
                        break;
' Program.cs

# 添加 DeletePluginStep 方法到文件末尾
cat >> Program.cs << 'METHOD'

    static void DeletePluginStep(EntityManager manager, string className, string messageName)
    {
        Console.WriteLine($">>> 删除 Plugin Step: {className} / {messageName}");
        // 这个方法是空的，实际删除逻辑需要在 EntityManager 中实现
    }
METHOD

dotnet build 2>&1 | grep -E "错误|error|失败" || echo "编译成功"
