import { Routes } from '@angular/router';

export const TASKS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./task-list/task-list.component').then((m) => m.TaskListComponent),
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./task-form/task-form.component').then((m) => m.TaskFormComponent),
  },
  {
    path: 'suggest',
    loadComponent: () =>
      import('./task-suggestion/task-suggestion.component').then(
        (m) => m.TaskSuggestionComponent,
      ),
  },
  {
    path: 'edit/:id',
    loadComponent: () =>
      import('./task-form/task-form.component').then((m) => m.TaskFormComponent),
  },
];
