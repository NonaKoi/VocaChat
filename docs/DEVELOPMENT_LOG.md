# 2026/07/13
## 初期采用手动管理，先不使用DI框架
目前管理对象较少，便于把握对象间关系。待需管理对象较多后再使用DI框架
---
# 2026/07/15
## 数据库是持久化数据的事实来源
对象数据可能会过时，数据应尽可能来源于数据库。除非特殊情况的确只需要对象中的快照

## 重构，加入Web API
Console 和 Web API 会变成两个独立的程序入口，但它们需要使用同一套业务代码，故进行结构调整
Models、Services、Data、Migrations、Properties放入VocaChat.Shared

## Web API 模板引入新的高危传递依赖。
Microsoft.AspNetCore.OpenApi 10.0.6
Microsoft.OpenApi 2.0.0
还原项目时，NuGet 报告 `Microsoft.OpenApi 2.0.0` 存在高危漏洞

只升级外层 `Microsoft.AspNetCore.OpenApi` 到 `10.0.10` 后，NuGet 仍然解析到有问题的 `Microsoft.OpenApi 2.0.0`，说明外层升级不足。

修复：
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.10" />
<PackageReference Include="Microsoft.OpenApi" Version="2.7.5" />

项目模板只能作为起点，不能默认认为模板生成的依赖组合就是当前安全组合。每次引入新 Host 或关键 NuGet 包，都应该检查完整的传递依赖
