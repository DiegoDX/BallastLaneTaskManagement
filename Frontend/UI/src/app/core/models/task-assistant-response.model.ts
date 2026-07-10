import { TaskAssistantAction } from './task-assistant-action.model';

export interface TaskAssistantResponse {
  content: string;
  model?: string | null;
  actions: TaskAssistantAction[];
}
