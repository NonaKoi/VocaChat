# VocaChat 项目计划

> 最后更新：2026-07-15
> 当前里程碑：v0.4.0

VocaChat 是一个本地优先的 AI 社交软件，整体形式接近传统即时通讯软件。用户可以创建长期存在的 AI 账号，并通过私聊和群聊与这些账号互动。

## v0.1.0 — Console 原型
已完成

* [x] 创建 AI 账号
* [x] 将 AI 账号保存在内存中
* [x] 创建群聊
* [x] 选择 AI 账号作为群成员
* [x] 发送用户消息
* [x] 选择 AI 账号回复
* [x] 生成模拟 AI 回复
* [x] 将群消息保存在内存中
* [x] 显示聊天记录
* [x] 将主要业务逻辑从 `Program.cs` 中拆出

## v0.2.0 — 核心业务与 Service 整理

已完成

* [x] 明确 `Models`、`Services`、控制台交互层和 `Program.cs` 的职责
* [x] 将 AI 账号创建、验证、保存和查询逻辑整理到 Service
* [x] 将群聊创建和群成员管理逻辑整理到 Service
* [x] 将用户消息和 AI 消息的创建、验证与保存逻辑整理到 Service
* [x] 将 AI 发言者选择和模拟回复生成逻辑整理到独立 Service
* [x] 确保 AI 回复只能由当前群成员产生
* [x] 确保群成员来自用户已经创建的 AI 账号
* [x] 将控制台输入、输出和流程组织从核心业务中分离
* [x] 将 `Program.cs` 简化为对象创建和程序启动入口
* [x] 限制外部代码直接修改账号、群成员和消息集合
* [x] 使核心业务逻辑能够脱离 Console 独立调用
* [x] 为主要业务规则补充单元测试

## v0.3.0 — SQLite 与 EF Core

已完成

* [x] 建立 EF Core 与 SQLite 基础环境
* [x] 创建 `VocaChatDbContext`
* [x] 配置数据库连接和 Migration
* [x] 将 AI 账号迁移到数据库
* [x] 将群聊迁移到数据库
* [x] 将群成员关系迁移到数据库
* [x] 将群消息迁移到数据库
* [x] 将核心 Service 从内存集合迁移到 EF Core
* [x] 支持程序重启后读取已有数据
* [x] 补充数据库集成测试

## v0.4.0 — ASP.NET Core Web API
进行中

* [x] 整理可供 Console 和 Web API 共享的业务代码
* [x] 保持 Console 原型继续正常运行
* [x] 创建 ASP.NET Core Web API 项目
* [x] 配置依赖注入、数据库和 OpenAPI
* [x] 实现 AI 账号 API
* [x] 实现群聊和群成员 API
* [x] 实现群消息和模拟 AI 回复 API
* [x] 使用 DTO 定义 HTTP 请求和响应
* [ ] 补充 HTTP API 集成测试
