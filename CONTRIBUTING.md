# 反馈与修改

朋友试用请先读 [快速指南](docs/FRIENDS_TEST_GUIDE.md)，再通过 Issues 描述问题或建议。截图请遮住个人任务；无需上传完整个人存档或系统日志。

准备修改源码时，请先与维护者确认使用范围，当前没有开源许可证。开发方式见 [开发说明](docs/DEVELOPMENT.zh-CN.md)。

每次修改建议围绕一个行为：说明触发条件与预期结果，在独立存档下验证，运行 PortableChecks 与团结引擎 21 组基线，涉及界面或计时再运行 Windows 程序。保留存档字段兼容和迁移，不修改个人 days.json。

提交中不要包含 Library、bin、obj、Windows 运行时、个人存档、许可证文件、凭据、原始日志或本机配置。提交源码与 .meta；发布二进制放 Releases。
