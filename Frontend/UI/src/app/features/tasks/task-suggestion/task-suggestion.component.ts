import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormArray,
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
  TaskSuggestionCreateRequest,
  TaskSuggestionTaskOverride,
} from '../../../core/models';
import { resolveHttpErrorMessage } from '../../../shared/helpers/http-error.helper';
import { TASK_SUGGESTION_MAX_BATCH_SIZE } from '../../../shared/helpers/task-suggestion.constants';

type ConfigureFormControls = {
  prompt: FormControl<string>;
  taskCount: FormControl<number>;
  dueDate: FormControl<string>;
};

type SlotFormControls = {
  title: FormControl<string>;
  description: FormControl<string>;
  dueDate: FormControl<string>;
};

type SlotFormValue = {
  title: string;
  description: string;
  dueDate: string;
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

  readonly maxBatchSize = TASK_SUGGESTION_MAX_BATCH_SIZE;

  readonly currentStep = signal<'configure' | 'review'>('configure');
  readonly isLoadingPreview = signal(false);
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly configureForm: FormGroup<ConfigureFormControls> =
    this.formBuilder.nonNullable.group({
      prompt: ['', Validators.required],
      taskCount: [
        3,
        [
          Validators.required,
          Validators.min(1),
          Validators.max(TASK_SUGGESTION_MAX_BATCH_SIZE),
        ],
      ],
      dueDate: [''],
    });

  readonly slotsForm: FormGroup<{ slots: FormArray<FormGroup<SlotFormControls>> }> =
    this.formBuilder.nonNullable.group({
      slots: this.formBuilder.array<FormGroup<SlotFormControls>>([]),
    });

  get slots(): FormArray<FormGroup<SlotFormControls>> {
    return this.slotsForm.controls.slots;
  }

  onContinue(): void {
    if (this.configureForm.invalid) {
      this.configureForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.rebuildSlotsFormArray(this.configureForm.controls.taskCount.value);
    this.currentStep.set('review');
  }

  onBack(): void {
    if (this.currentStep() === 'review') {
      this.errorMessage.set(null);
      this.currentStep.set('configure');
      return;
    }

    void this.router.navigate(['/tasks']);
  }

  onPreviewSample(): void {
    if (this.isLoadingPreview() || this.isPreviewSampleDisabled()) {
      return;
    }

    const prompt = this.configureForm.controls.prompt.value.trim();
    if (!prompt) {
      this.errorMessage.set('Prompt is required to preview a sample suggestion.');
      return;
    }

    this.errorMessage.set(null);
    this.isLoadingPreview.set(true);

    this.taskSuggestionService
      .getSuggestionPreview({ prompt })
      .pipe(
        finalize(() => this.isLoadingPreview.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (response) => {
          const firstSlot = this.slots.at(0);
          if (!firstSlot) {
            return;
          }

          firstSlot.patchValue({
            title: response.title,
            description: response.description ?? '',
          });
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(
              error,
              'Failed to load suggestion preview.',
            ),
          );
        },
      });
  }

  onCreate(): void {
    if (this.isSubmitting()) {
      return;
    }

    const validationError = this.validateBeforeCreate();
    if (validationError) {
      this.errorMessage.set(validationError);
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    const request = this.buildCreateRequest();

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

  isConfigureFieldInvalid(fieldName: keyof ConfigureFormControls): boolean {
    const control = this.configureForm.controls[fieldName];
    return control.invalid && (control.dirty || control.touched);
  }

  isPreviewSampleDisabled(): boolean {
    const firstSlot = this.slots.at(0);
    if (!firstSlot) {
      return true;
    }

    return firstSlot.controls.title.value.trim().length > 0;
  }

  slotLabel(index: number): string {
    const slot = this.slots.at(index);
    const hasTitle = (slot?.controls.title.value.trim().length ?? 0) > 0;
    return hasTitle ? `Task ${index + 1} (manual)` : `Task ${index + 1} (AI)`;
  }

  private rebuildSlotsFormArray(taskCount: number): void {
    const existingValues = this.slots.controls.map((group) => group.getRawValue());

    this.slots.clear();

    for (let index = 0; index < taskCount; index++) {
      const preserved = existingValues[index];
      this.slots.push(this.createSlotGroup(preserved));
    }
  }

  private createSlotGroup(
    value?: Partial<SlotFormValue>,
  ): FormGroup<SlotFormControls> {
    return this.formBuilder.nonNullable.group({
      title: [value?.title ?? ''],
      description: [value?.description ?? ''],
      dueDate: [value?.dueDate ?? ''],
    });
  }

  private validateBeforeCreate(): string | null {
    const taskCount = this.configureForm.controls.taskCount.value;

    if (taskCount < 1 || taskCount > TASK_SUGGESTION_MAX_BATCH_SIZE) {
      return `Task count must be between 1 and ${TASK_SUGGESTION_MAX_BATCH_SIZE}.`;
    }

    if (this.slots.length > taskCount) {
      return 'Too many task slots for the configured count.';
    }

    const hasLlmSlot = this.slots.controls.some(
      (slot) => slot.controls.title.value.trim().length === 0,
    );

    if (hasLlmSlot && !this.configureForm.controls.prompt.value.trim()) {
      return 'Prompt is required when one or more tasks will be generated by AI.';
    }

    return null;
  }

  private buildCreateRequest(): TaskSuggestionCreateRequest {
    const configureValue = this.configureForm.getRawValue();
    const taskCount = configureValue.taskCount;
    const prompt = configureValue.prompt.trim();
    const globalDueDate = configureValue.dueDate.trim();

    const request: TaskSuggestionCreateRequest = {
      taskCount,
      prompt: prompt || undefined,
    };

    if (globalDueDate) {
      request.dueDate = new Date(globalDueDate).toISOString();
    }

    const tasks = this.mapSlotsToOverrides(taskCount);
    const hasAnySlotOverride = tasks.some(
      (override) => Object.keys(override).length > 0,
    );

    if (hasAnySlotOverride) {
      request.tasks = tasks;
    }

    return request;
  }

  private mapSlotsToOverrides(taskCount: number): TaskSuggestionTaskOverride[] {
    const overrides: TaskSuggestionTaskOverride[] = [];

    for (let index = 0; index < taskCount; index++) {
      const slot = this.slots.at(index);
      const slotValue = slot?.getRawValue() ?? {
        title: '',
        description: '',
        dueDate: '',
      };

      overrides.push(this.mapSlotToOverride(slotValue));
    }

    return overrides;
  }

  private mapSlotToOverride(
    slot: SlotFormValue,
  ): TaskSuggestionTaskOverride {
    const override: TaskSuggestionTaskOverride = {};
    const title = slot.title.trim();
    const description = slot.description.trim();
    const dueDate = slot.dueDate.trim();

    if (title) {
      override.title = title;
    }

    if (description) {
      override.description = description;
    }

    if (dueDate) {
      override.dueDate = new Date(dueDate).toISOString();
    }

    return override;
  }
}
