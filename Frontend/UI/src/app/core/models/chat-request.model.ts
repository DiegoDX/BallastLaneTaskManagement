import { ChatMessage } from './chat-message.model';

export interface ChatRequest {
  messages: ChatMessage[];
}
