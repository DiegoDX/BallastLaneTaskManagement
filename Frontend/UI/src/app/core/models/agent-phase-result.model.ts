export interface AgentPhaseResult {
  phase: string;
  status: string;
  outputJson?: string | null;
  durationMs?: number | null;
}

export interface AgentToolCallRecord {
  name: string;
  success: boolean;
}

export interface AgentExecutionReport {
  iterations: number;
  toolCalls: AgentToolCallRecord[];
}
