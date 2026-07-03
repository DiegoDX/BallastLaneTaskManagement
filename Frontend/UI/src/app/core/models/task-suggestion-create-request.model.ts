import { TaskSuggestionTaskOverride } from './task-suggestion-task-override.model';

export interface TaskSuggestionCreateRequest {
  prompt?: string;
  taskCount: number;
  dueDate?: string;
  tasks?: TaskSuggestionTaskOverride[];
}
