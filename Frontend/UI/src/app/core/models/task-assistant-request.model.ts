import { TaskAssistantMessage } from './task-assistant-message.model';

export interface TaskAssistantRequest {
  messages: TaskAssistantMessage[];
}
