# PortableChecks

无需安装 Unity 的 C# 业务逻辑检查。需要 **.NET 10 SDK**，不依赖第三方 NuGet 包。

从仓库根目录运行：

```powershell
dotnet run --project tests/PortableChecks -c Release
```

默认报告保存到当前目录的 `work/portable-checks/report.txt`。也可指定报告位置：

```powershell
dotnet run --project tests/PortableChecks -c Release -- work/portable-checks/ci-report.txt
```

退出码：`0` 表示全部检查通过；`1` 表示至少一组检查失败；`2` 表示参数或运行环境错误。每次运行创建独立测试存档，只在报告所在目录写入，不读取正式用户存档。

项目直接引用 `UnityProject/Assets/Scripts` 中的 `TaskDomain.cs`、`StudyDomain.cs`、`CoreChecks.cs` 和 `StudyChecks.cs`，运行同一套 **21 组基线检查**。时间通过小步模拟推进，不需要等待真实番茄钟完成。

`UnityStandIns.cs` 以 `System.Text.Json` 适配少量 `JsonUtility` / `Debug` 调用。**这个序列化器不能代替 Unity 原生 JSON 验证**，尤其不能保证缺省字段、对象初始化或异常输入行为完全一致。发布前仍需在当前 Unity/团结引擎中运行原生检查、构建 Windows 程序，并执行窗口和交互回归。

这些适配文件应保留在 `tests` 中，不要复制到 Unity 的 `Assets` 目录，避免与真正的 `UnityEngine` 类型冲突。

如源码位于不同目录，可以覆盖路径而不用修改核心代码：

```powershell
dotnet run --project tests/PortableChecks -p:UnityProjectPath="C:/path/to/UnityProject" -- work/portable-checks/report.txt
```
