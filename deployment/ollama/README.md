# VocaChat 本地模型

当前本地试行模型使用 Ollama 和 `qwen3.5:4b`。程序目录与模型目录保持分离：

```text
D:\Ollama
├─ ollama.exe
└─ models
```

当前用户环境变量：

```text
OLLAMA_MODELS=D:\Ollama\models
```

首次准备模型：

```powershell
ollama pull qwen3.5:4b
ollama create vocachat-qwen3.5-4b -f deployment/ollama/VocaChat.Modelfile
ollama list
```

本地服务默认监听 `http://127.0.0.1:11434`。VocaChat 通过 OpenAI 兼容接口访问：

```text
http://127.0.0.1:11434/v1/chat/completions
```

当前桌面壳尚未负责进程生命周期；Windows 重新启动后如 Ollama 未自动运行，可先执行：

```powershell
ollama serve
```

可使用以下环境变量覆盖默认模型连接：

```text
VOCACHAT_AI_BASE_URL
VOCACHAT_AI_MODEL
VOCACHAT_AI_API_KEY
```

更换为其他 OpenAI 兼容服务时，不需要修改 AI 账号、关系、会话或消息数据。
