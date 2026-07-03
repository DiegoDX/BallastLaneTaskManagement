import { TaskItemStatus } from '../../../core/models';

export const TASK_STATUS_OPTIONS: readonly TaskItemStatus[] = [
  'Pending',
  'InProgress',
  'Completed',
] as const;

export function formatTaskStatus(status: TaskItemStatus): string {
  switch (status) {
    case 'InProgress':
      return 'In Progress';
    case 'Pending':
      return 'Pending';
    case 'Completed':
      return 'Completed';
  }
}

export function formatDueDate(isoDate: string): string {
  return new Date(isoDate).toLocaleDateString();
}
