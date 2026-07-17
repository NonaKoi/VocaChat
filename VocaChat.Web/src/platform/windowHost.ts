export interface VocaChatWindowHost { minimize(): void; maximize(): void; close(): void }

declare global { interface Window { vocaChatWindow?: VocaChatWindowHost } }

export function getWindowHost(): VocaChatWindowHost | undefined { return window.vocaChatWindow }
