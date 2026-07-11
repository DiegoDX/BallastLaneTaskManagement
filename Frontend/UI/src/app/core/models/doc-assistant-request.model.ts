import { DocAssistantMessage } from './doc-assistant-message.model';

export interface DocAssistantRequest {
  messages: DocAssistantMessage[];
}
