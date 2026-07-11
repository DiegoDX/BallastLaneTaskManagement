export interface AgentPlanStep {
  order: number;
  description: string;
  toolHint?: string | null;
}

export interface AgentPlan {
  goal: string;
  steps: AgentPlanStep[];
  requiresApproval: boolean;
  riskLevel: string;
}
