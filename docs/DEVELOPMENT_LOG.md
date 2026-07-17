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

## 添加群成员采用已有业务流程
FindById(groupChatId) -> TryAddMember(groupChat, aiAccountId)
这个流程会产生一次额外查询,但当前更重视现有 Service 行为清晰、容易理解，暂不为了微小查询优化扩展业务 API

---
# 2026/07/17
## AI 好友关系采用有方向模型
同一对好友分别保存 `A → B` 和 `B → A`，因为双方的熟悉度、好感度和信任度可能不同。关系判断器以后应同时读取两个方向，而不是把关系压缩为一个对称总分。

## 默认关系采用惰性持久化
未配置的好友组合由 Service 返回中性默认值，普通查询不会创建数据库记录。只有用户保存关系或实际互动发生时才持久化，避免好友数量增长后预先生成大量无意义关系行。

## 自主私信判断先执行硬规则，再进行可解释评分
全局开关、私信许可、双方个人设置、发起权限和冷却时间属于不能被分数绕过的硬规则。通过硬规则后，再按熟悉度、好感度、信任度和主动性计算候选发起者及最终分数，使以后调整权重时不会破坏权限边界。

## 随机性只作为有限扰动
随机值限制在 `-10` 到 `+10`，只让接近门槛的互动保留一定自然波动，不能把低关系分直接变成高概率互动。判断器显式接收时间和随机值并保持只读，便于稳定测试，也避免“预览判断”意外创建会话或消息。
