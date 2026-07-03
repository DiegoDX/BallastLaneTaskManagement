import { TaskItem } from './task-item.model';

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
}

export interface TaskSearchParams {
  pageNumber?: number;
  pageSize?: number;
  title?: string;
  status?: string;
  sortBy?: string;
  sortDirection?: string;
}

export type PagedTaskResult = PagedResult<TaskItem>;
