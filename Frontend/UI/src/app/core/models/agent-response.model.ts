import { TaskAssistantAction } from './task-assistant-action.model';
import { AgentPlan } from './agent-plan.model';
import { AgentPhaseResult, AgentExecutionReport } from './agent-phase-result.model';

export interface AgentResponse {
  summary: string;
  phases: AgentPhaseResult[];
  actions: TaskAssistantAction[];
  executionReport?: AgentExecutionReport | null;
  plan?: AgentPlan | null;
  status: string;
  runId?: string | null;
  model?: string | null;
}
