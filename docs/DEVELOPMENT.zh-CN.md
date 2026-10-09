# 开发与构建

正式试玩下载 Release，开发使用仓库内 UnityProject。当前验证版本为团结引擎 2022.3.61t14、Visual Studio Community 18.10.3 和 Windows x64。其他 Unity 2022.3 版本请先复制项目再导入验收。

## Unity / 团结引擎

1. 在 Hub 添加 UnityProject，使用已安装并激活的编辑器打开。
2. 菜单 Mochi Day → 准备场景与窗口设置，按 Play 运行。
3. 运行 Mochi Day → 运行存档与日期测试。
4. 运行 Mochi Day → 构建 Windows x64，完整程序位于仓库根 Windows 目录。

界面和角色使用 C# 绘制；启动组件由运行时自动创建。主要界面不在 Canvas Inspector 中修改。

| 修改内容 | 文件 |
| --- | --- |
| 主窗口、任务和公共样式 | UnityProject/Assets/Scripts/MochiApp.cs |
| 番茄钟、商店、统计、弹窗 | UnityProject/Assets/Scripts/StudyViews.cs |
| 任务、历史、存档和迁移 | UnityProject/Assets/Scripts/TaskDomain.cs |
| 计时、金币、角色和物品规则 | UnityProject/Assets/Scripts/StudyDomain.cs |
| 角色外形、服饰与动画 | UnityProject/Assets/Scripts/PetRenderer.cs |
| Windows 自身窗口操作 | UnityProject/Assets/Scripts/WindowsWindow.cs |
| 构建设置与图标 | UnityProject/Assets/Editor/BuildProject.cs |

保留 MochiDay 命名空间和存档兼容逻辑。升级结构时增加明确迁移与测试。

## Visual Studio

可在引擎中双击脚本并使用引擎生成的解决方案。仓库另附 MochiStudy.sln 与辅助 MochiDay.Code.csproj，用于编辑、跳转与编译检查。

辅助工程没有写死本机安装目录。复制根目录 Directory.Build.props.example 为 Directory.Build.props，填入你的引擎 Editor/Data/Managed/UnityEngine 目录；此文件不提交。也可设置环境变量 UNITY_MANAGED_PATH 或构建属性 UnityManagedPath。然后打开 MochiStudy.sln 并编译。

辅助工程不能直接 F5 启动游戏：请在引擎按 Play 或运行 Windows/MochiStudy.exe。IDE 集成已安装时可附加 Unity 调试。

## 无需引擎的规则检查

安装 .NET 10 SDK，运行：

    dotnet run --project tests/PortableChecks/PortableChecks.csproj --configuration Release

默认报告在 work/portable-checks/report.txt；共有 21 组检查。该项目采用 JSON 适配层，不能替代引擎原生 JSON 和 Windows 交互验收。GitHub Actions 仅运行这层检查，不需要 Unity 云构建凭据。

## 命令行构建

PowerShell 示例，将路径替换为你的已激活编辑器：

    ./scripts/Build-Windows.ps1 -EditorPath 'C:\YourEditor\Editor\Tuanjie.exe'

脚本先执行引擎检查，再构建 Windows。日志保留在 artifacts，不提交仓库。构建后的 Windows 目录须完整分发；引擎许可证需由使用者自行配置。

## 隔离试玩

    ./Windows/MochiStudy.exe --data-dir 'C:\YourTestFolder'

默认正式存档在本机 LOCALAPPDATA/MochiDay；只在独立测试目录模拟奖励、日期与损坏恢复。Release 包不带演示存档。程序内部版本 2.0.0，对外试用标签 v2.0.0-beta.1。
