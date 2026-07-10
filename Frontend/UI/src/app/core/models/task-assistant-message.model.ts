export type TaskAssistantRole = 'user' | 'assistant';

export interface TaskAssistantMessage {
  role: TaskAssistantRole;
  content: string;
}
