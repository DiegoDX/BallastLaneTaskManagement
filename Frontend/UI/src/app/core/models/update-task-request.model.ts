import { TaskItemStatus } from './task-item.model';

export interface UpdateTaskRequest {
  title: string;
   description?: string | null;
  status: TaskItemStatus;
}
