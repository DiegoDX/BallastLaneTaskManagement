export type DocAssistantRole = 'user' | 'assistant';

export interface DocAssistantMessage {
  role: DocAssistantRole;
  content: string;
}
