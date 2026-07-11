import { DocAssistantSource } from './doc-assistant-source.model';

export interface DocAssistantResponse {
  content: string;
  sources: DocAssistantSource[];
  model?: string | null;
}
