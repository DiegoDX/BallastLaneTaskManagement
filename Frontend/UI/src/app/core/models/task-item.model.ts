export type TaskItemStatus = 'Pending' | 'InProgress' | 'Completed';

export interface TaskItem {
  id: string;
  userId: string;
  title: string;
  description: string | null;
  status: TaskItemStatus;
  dueDate: string;
  createdDate?: string;
}
