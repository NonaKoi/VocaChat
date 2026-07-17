# VocaChat Web

VocaChat 的 React、TypeScript 与 Vite 前端。

开发时先启动 Web API 的 HTTP profile：

```powershell
dotnet run --project VocaChat.WebApi --launch-profile http
```

再从仓库根目录启动前端：

```powershell
npm install --prefix VocaChat.Web
npm run dev --prefix VocaChat.Web
```

前端统一请求相对地址 `/api/...`，Vite 会将其代理到
`http://localhost:5205`。当前不需要修改 Web API 的 CORS 配置。
