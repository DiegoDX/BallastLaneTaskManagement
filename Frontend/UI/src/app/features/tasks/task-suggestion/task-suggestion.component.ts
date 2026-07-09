import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { TaskSuggestionService } from '../../../core/services/task-suggestion.service';
import {
  TaskSuggestionBatchItem,
  TaskSuggestionCreateRequest,
} from '../../../core/models';
import { resolveHttpErrorMessage } from '../../../shared/helpers/http-error.helper';

type GenerateFormControls = {
  prompt: FormControl<string>;
};

@Component({
  selector: 'app-task-suggestion',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './task-suggestion.component.html',
  styleUrl: './task-suggestion.component.css',
})
export class TaskSuggestionComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly taskSuggestionService = inject(TaskSuggestionService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly hasPreview = signal(false);
  readonly isGenerating = signal(false);
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly previewJson = signal('');

  readonly generateForm: FormGroup<GenerateFormControls> =
    this.formBuilder.nonNullable.group({
      prompt: ['', Validators.required],
    });

  onGenerate(): void {
    if (this.isGenerating()) {
      return;
    }

    if (this.generateForm.invalid) {
      this.generateForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.isGenerating.set(true);

    const { prompt } = this.generateForm.getRawValue();

    this.taskSuggestionService
      .generateBatch({ prompt: prompt.trim() })
      .pipe(
        finalize(() => this.isGenerating.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (response) => {
          this.previewJson.set(
            JSON.stringify({ tasks: response.tasks }, null, 2),
          );
          this.hasPreview.set(true);
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(error, 'Failed to generate tasks.'),
          );
        },
      });
  }

  onRegenerate(): void {
    this.onGenerate();
  }

  onSaveAll(): void {
    if (this.isSubmitting() || !this.hasPreview()) {
      return;
    }

    const validationResult = this.validateJsonBeforeSave(this.previewJson());
    if (typeof validationResult === 'string') {
      this.errorMessage.set(validationResult);
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    const request: TaskSuggestionCreateRequest = {
      tasks: validationResult.tasks,
    };

    this.taskSuggestionService
      .createFromSuggestions(request)
      .pipe(
        finalize(() => this.isSubmitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (tasks) => {
          const count = tasks.length;
          const message =
            count === 1
              ? '1 task created successfully.'
              : `${count} tasks created successfully.`;

          void this.router.navigate(['/tasks'], {
            state: { successMessage: message },
          });
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(error, 'Failed to create tasks.'),
          );
        },
      });
  }

  onBackToTasks(): void {
    void this.router.navigate(['/tasks']);
  }

  onPreviewJsonInput(event: Event): void {
    this.previewJson.set((event.target as HTMLTextAreaElement).value);
  }

  isGenerateFieldInvalid(fieldName: keyof GenerateFormControls): boolean {
    const control = this.generateForm.controls[fieldName];
    return control.invalid && (control.dirty || control.touched);
  }

  private validateJsonBeforeSave(
    jsonText: string,
  ): { tasks: TaskSuggestionBatchItem[] } | string {
    let parsed: unknown;

    try {
      parsed = JSON.parse(jsonText);
    } catch {
      return 'Invalid JSON. Please fix the syntax before saving.';
    }

    if (
      !parsed ||
      typeof parsed !== 'object' ||
      !('tasks' in parsed)
    ) {
      return 'JSON must be an object with a "tasks" property.';
    }

    const tasks = (parsed as { tasks: unknown }).tasks;
    if (!Array.isArray(tasks) || tasks.length === 0) {
      return '"tasks" must be a non-empty array.';
    }

    const normalized: TaskSuggestionBatchItem[] = [];

    for (let index = 0; index < tasks.length; index++) {
      const item = tasks[index];
      if (!item || typeof item !== 'object') {
        return `Task ${index + 1} must be an object.`;
      }

      const title = (item as { title?: unknown }).title;
      if (typeof title !== 'string' || !title.trim()) {
        return `Task ${index + 1} requires a non-empty title.`;
      }

      const description = (item as { description?: unknown }).description;
      normalized.push({
        title: title.trim(),
        description:
          typeof description === 'string' ? description.trim() : '',
      });
    }

    return { tasks: normalized };
  }
}
