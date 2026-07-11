import { AgentMessage } from './agent-message.model';

export interface AgentRequest {
  messages: AgentMessage[];
}
