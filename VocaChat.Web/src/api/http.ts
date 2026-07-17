interface ApiErrorOptions {
  status?: number
  isNetworkError?: boolean
  responseBody?: unknown
}

interface ErrorResponseBody {
  message?: unknown
  detail?: unknown
  title?: unknown
}

/** 表示浏览器无法连接后端，或后端返回了非成功 HTTP 状态。 */
export class ApiError extends Error {
  readonly status?: number
  readonly isNetworkError: boolean
  readonly responseBody?: unknown

  constructor(message: string, options: ApiErrorOptions = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = options.status
    this.isNetworkError = options.isNetworkError ?? false
    this.responseBody = options.responseBody
  }
}

/** 通过相对地址读取 JSON，并把网络和 HTTP 错误转换成可显示的信息。 */
export async function getJson<T>(path: string): Promise<T> {
  return requestJson<T>(path, {
    headers: {
      Accept: 'application/json',
    },
  })
}

/** 通过相对地址提交 JSON，并保留后端返回的结构化失败信息。 */
export async function postJson<TResponse, TRequest>(
  path: string,
  body: TRequest,
): Promise<TResponse> {
  return requestJson<TResponse>(path, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  })
}

async function requestJson<T>(path: string, init: RequestInit): Promise<T> {
  let response: Response

  try {
    response = await fetch(path, init)
  } catch {
    throw new ApiError('无法连接 VocaChat Web API，请确认后端已经启动。', {
      isNetworkError: true,
    })
  }

  if (!response.ok) {
    const isGatewayError = [502, 503, 504].includes(response.status)
    const errorDetails = await readErrorDetails(response)
    const errorMessage = isGatewayError
      ? `无法连接 VocaChat Web API（HTTP ${response.status}），请确认后端已经启动。`
      : errorDetails.message

    throw new ApiError(errorMessage, {
      status: response.status,
      isNetworkError: isGatewayError,
      responseBody: errorDetails.body,
    })
  }

  try {
    return (await response.json()) as T
  } catch {
    throw new ApiError('Web API 返回了无法解析的 JSON 数据。', {
      status: response.status,
    })
  }
}

async function readErrorDetails(
  response: Response,
): Promise<{ message: string; body?: unknown }> {
  const fallbackMessage = `请求失败（HTTP ${response.status}）。`
  const responseText = await response.text()

  if (!responseText) {
    return { message: fallbackMessage }
  }

  try {
    const body = JSON.parse(responseText) as ErrorResponseBody
    return {
      message:
        getNonEmptyString(body.message) ??
        getNonEmptyString(body.detail) ??
        getNonEmptyString(body.title) ??
        fallbackMessage,
      body,
    }
  } catch {
    return { message: fallbackMessage }
  }
}

function getNonEmptyString(value: unknown): string | undefined {
  if (typeof value !== 'string' || value.trim().length === 0) {
    return undefined
  }

  return value
}
