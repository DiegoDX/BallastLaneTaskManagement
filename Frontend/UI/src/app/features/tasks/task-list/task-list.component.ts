import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { TaskService } from '../../../core/services/task.service';
import { TaskItem } from '../../../core/models';
import { resolveHttpErrorMessage } from '../../../shared/helpers/http-error.helper';
import {
  formatDueDate,
  formatTaskStatus,
} from '../helpers/task-display.helper';

@Component({
  selector: 'app-task-list',
  standalone: true,
  imports: [],
  templateUrl: './task-list.component.html',
  styleUrl: './task-list.component.css',
})
export class TaskListComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly taskService = inject(TaskService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly tasks = signal<TaskItem[]>([]);
  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly deletingTaskId = signal<string | null>(null);

  readonly formatTaskStatus = formatTaskStatus;
  readonly formatDueDate = formatDueDate;

  ngOnInit(): void {
    this.readNavigationSuccessMessage();
    this.loadTasks();
  }

  loadTasks(): void {
    if (this.isLoading()) {
      return;
    }

    this.errorMessage.set(null);
    this.isLoading.set(true);

    this.taskService
      .getTasks({ pageNumber: 1, pageSize: 100 })
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (result) => this.tasks.set(result.items),
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(error, 'Failed to load tasks.'),
          );
        },
      });
  }

  onDeleteTask(task: TaskItem): void {
    if (this.deletingTaskId() !== null) {
      return;
    }

    const confirmed = window.confirm(
      `Are you sure you want to delete "${task.title}"?`,
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.deletingTaskId.set(task.id);

    this.taskService
      .deleteTask(task.id)
      .pipe(
        finalize(() => this.deletingTaskId.set(null)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          this.successMessage.set('Task deleted successfully.');
          this.loadTasks();
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(error, 'Failed to delete task.'),
          );
        },
      });
  }

  logout(): void {
    this.authService.logout().subscribe({
      next: () => {
        void this.router.navigate(['/login']);
      },
      error: () => {
        this.authService.clearSession();
        void this.router.navigate(['/login']);
      },
    });
  }

  navigateToCreate(): void {
    void this.router.navigate(['/tasks/new']);
  }

  navigateToSuggest(): void {
    void this.router.navigate(['/tasks/suggest']);
  }

  navigateToChat(): void {
    void this.router.navigate(['/chat']);
  }

  navigateToEdit(taskId: string): void {
    void this.router.navigate(['/tasks/edit', taskId]);
  }

  private readNavigationSuccessMessage(): void {
    const navigation = this.router.getCurrentNavigation();
    const stateMessage = navigation?.extras.state?.['successMessage'];

    if (typeof stateMessage === 'string' && stateMessage.length > 0) {
      this.successMessage.set(stateMessage);
      return;
    }

    const historyState = history.state as { successMessage?: string };
    if (historyState.successMessage) {
      this.successMessage.set(historyState.successMessage);
    }
  }
}
