export type TaskAssistantActionType =
  | 'created'
  | 'listed'
  | 'updated'
  | 'deleted';

export interface TaskAssistantAction {
  type: TaskAssistantActionType;
  taskId?: string | null;
  title?: string | null;
  status?: string | null;
  dueDate?: string | null;
}
