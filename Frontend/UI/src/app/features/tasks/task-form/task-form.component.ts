import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { TaskService } from '../../../core/services/task.service';
import {
  CreateTaskRequest,
  TaskItemStatus,
  UpdateTaskRequest,
} from '../../../core/models';
import { resolveHttpErrorMessage } from '../../../shared/helpers/http-error.helper';
import { TASK_STATUS_OPTIONS } from '../helpers/task-display.helper';

type TaskFormControls = {
  title: FormControl<string>;
  description: FormControl<string>;
  status: FormControl<TaskItemStatus>;
  dueDate: FormControl<string>;
};

@Component({
  selector: 'app-task-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './task-form.component.html',
  styleUrl: './task-form.component.css',
})
export class TaskFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly taskService = inject(TaskService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly statusOptions = TASK_STATUS_OPTIONS;

  readonly form: FormGroup<TaskFormControls> = this.formBuilder.nonNullable.group({
    title: ['', Validators.required],
    description: [''],
    status: ['Pending' as TaskItemStatus, Validators.required],
    dueDate: ['', Validators.required],
  });

  readonly isEditMode = signal(false);
  readonly isLoading = signal(false);
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  private taskId: string | null = null;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.isEditMode.set(id !== null);
    this.taskId = id;

    if (this.isEditMode()) {
      this.configureEditForm();
      this.loadTask(id!);
      return;
    }

    this.setDefaultDueDate();
  }

  onSubmit(): void {
    if (this.isSubmitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    if (this.isEditMode() && this.taskId) {
      this.updateTask(this.taskId);
      return;
    }

    this.createTask();
  }

  isFieldInvalid(fieldName: keyof TaskFormControls): boolean {
    const control = this.form.controls[fieldName];
    return control.invalid && (control.dirty || control.touched);
  }

  private configureEditForm(): void {
    this.form.controls.dueDate.clearValidators();
    this.form.controls.dueDate.updateValueAndValidity();
  }

  private setDefaultDueDate(): void {
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    this.form.controls.dueDate.setValue(this.toDateInputValue(tomorrow));
  }

  private loadTask(id: string): void {
    this.isLoading.set(true);

    this.taskService
      .getTaskById(id)
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (task) => {
          this.form.patchValue({
            title: task.title,
            description: task.description ?? '',
            status: task.status,
          });
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(error, 'Failed to load task.'),
          );
        },
      });
  }

  private createTask(): void {
    const formValue = this.form.getRawValue();
    const request: CreateTaskRequest = {
      title: formValue.title.trim(),
      description: formValue.description.trim() || null,
      dueDate: new Date(formValue.dueDate).toISOString(),
    };

    this.taskService
      .createTask(request)
      .pipe(
        finalize(() => this.isSubmitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          void this.router.navigate(['/tasks'], {
            state: { successMessage: 'Task created successfully.' },
          });
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(error, 'Failed to create task.'),
          );
        },
      });
  }

  private updateTask(id: string): void {
    const formValue = this.form.getRawValue();
    const request: UpdateTaskRequest = {
      title: formValue.title.trim(),
      description: formValue.description,
      status: formValue.status,
    };

    this.taskService
      .updateTask(id, request)
      .pipe(
        finalize(() => this.isSubmitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          void this.router.navigate(['/tasks'], {
            state: { successMessage: 'Task updated successfully.' },
          });
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(error, 'Failed to update task.'),
          );
        },
      });
  }

  private toDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
