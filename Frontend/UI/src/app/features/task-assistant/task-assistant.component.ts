import {
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormControl,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { TaskAssistantService } from '../../core/services/task-assistant.service';
import {
  TaskAssistantAction,
  TaskAssistantMessage,
} from '../../core/models';
import { resolveHttpErrorMessage } from '../../shared/helpers/http-error.helper';
import { formatDueDate } from '../tasks/helpers/task-display.helper';

interface TaskAssistantDisplayMessage extends TaskAssistantMessage {
  actions?: TaskAssistantAction[];
}

@Component({
  selector: 'app-task-assistant',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './task-assistant.component.html',
  styleUrl: './task-assistant.component.css',
})
export class TaskAssistantComponent {
  private readonly taskAssistantService = inject(TaskAssistantService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messagesContainer =
    viewChild<ElementRef<HTMLElement>>('messagesContainer');

  readonly messages = signal<TaskAssistantDisplayMessage[]>([]);
  readonly isSending = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly suggestedPrompts = [
    'Create a task "Buy milk" due tomorrow',
    'Add a task to prepare the demo for next Friday',
    'Create a task "Call dentist" due today',
  ] as const;

  readonly messageControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required],
  });

  constructor() {
    effect(() => {
      this.messages();
      this.isSending();
      queueMicrotask(() => this.scrollToBottom());
    });
  }

  onSend(): void {
    if (this.isSending()) {
      return;
    }

    const content = this.messageControl.value.trim();
    if (!content) {
      this.messageControl.markAsTouched();
      return;
    }

    this.sendMessage(content);
  }

  onSuggestedPrompt(prompt: string): void {
    if (this.isSending()) {
      return;
    }

    this.sendMessage(prompt);
  }

  formatAction(action: TaskAssistantAction): string {
    const title = action.title ?? 'Task';

    switch (action.type) {
      case 'created':
        return action.dueDate
          ? `Created: ${title} (due ${formatDueDate(action.dueDate)})`
          : `Created: ${title}`;
      case 'listed':
        return action.status
          ? `Listed: ${title} (${action.status})`
          : `Listed: ${title}`;
      case 'updated':
        return action.status
          ? `Updated: ${title} (${action.status})`
          : `Updated: ${title}`;
      case 'deleted':
        return `Deleted: ${title}`;
    }
  }

  onMessageKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.onSend();
    }
  }

  private sendMessage(content: string): void {
    const userMessage: TaskAssistantMessage = { role: 'user', content };
    const updatedMessages = [...this.messages(), userMessage];

    this.messages.set(updatedMessages);
    this.messageControl.reset();
    this.errorMessage.set(null);
    this.isSending.set(true);

    const apiMessages: TaskAssistantMessage[] = updatedMessages.map(
      ({ role, content: messageContent }) => ({
        role,
        content: messageContent,
      }),
    );

    this.taskAssistantService
      .assist(apiMessages)
      .pipe(
        finalize(() => this.isSending.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (response) => {
          this.messages.update((current) => [
            ...current,
            {
              role: 'assistant',
              content: response.content,
              actions:
                response.actions.length > 0 ? response.actions : undefined,
            },
          ]);
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(error, 'Failed to get a response.'),
          );
        },
      });
  }

  private scrollToBottom(): void {
    const container = this.messagesContainer()?.nativeElement;
    if (!container) {
      return;
    }

    container.scrollTop = container.scrollHeight;
  }
}
