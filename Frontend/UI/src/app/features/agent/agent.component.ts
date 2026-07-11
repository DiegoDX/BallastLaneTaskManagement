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
import { AgentService } from '../../core/services/agent.service';
import {
  AgentMessage,
  AgentPhaseResult,
  AgentPlan,
  AgentResponse,
  TaskAssistantAction,
} from '../../core/models';
import { resolveHttpErrorMessage } from '../../shared/helpers/http-error.helper';
import { formatDueDate } from '../tasks/helpers/task-display.helper';

interface AgentDisplayMessage extends AgentMessage {
  actions?: TaskAssistantAction[];
  phases?: AgentPhaseResult[];
  plan?: AgentPlan | null;
  executionReportExpanded?: boolean;
}

@Component({
  selector: 'app-agent',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './agent.component.html',
  styleUrl: './agent.component.css',
})
export class AgentComponent {
  private readonly agentService = inject(AgentService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messagesContainer =
    viewChild<ElementRef<HTMLElement>>('messagesContainer');

  readonly messages = signal<AgentDisplayMessage[]>([]);
  readonly isSending = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly pendingRunId = signal<string | null>(null);
  readonly pendingPlan = signal<AgentPlan | null>(null);
  readonly lastPhases = signal<AgentPhaseResult[]>([]);

  readonly suggestedPrompts = [
    'Organize my tasks by due date',
    'Tomorrow I need to study history — set up my tasks',
    'Show pending tasks and update overdue ones to InProgress',
  ] as const;

  readonly phaseOrder = ['Plan', 'Approval', 'Execute', 'Review', 'Summary'] as const;

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

  onApprovePlan(): void {
    this.continueRun(true);
  }

  onRejectPlan(): void {
    this.continueRun(false);
  }

  toggleExecutionReport(messageIndex: number): void {
    this.messages.update((current) =>
      current.map((message, index) =>
        index === messageIndex
          ? {
              ...message,
              executionReportExpanded: !message.executionReportExpanded,
            }
          : message,
      ),
    );
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
      default:
        return `${action.type}: ${title}`;
    }
  }

  phaseStatusLabel(phaseName: string): string {
    const phase = this.lastPhases().find((item) => item.phase === phaseName);
    return phase?.status ?? 'Pending';
  }

  onMessageKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.onSend();
    }
  }

  private sendMessage(content: string): void {
    const userMessage: AgentMessage = { role: 'user', content };
    const updatedMessages = [...this.messages(), userMessage];

    this.messages.set(updatedMessages);
    this.messageControl.reset();
    this.errorMessage.set(null);
    this.pendingRunId.set(null);
    this.pendingPlan.set(null);
    this.isSending.set(true);

    const apiMessages: AgentMessage[] = updatedMessages.map(
      ({ role, content: messageContent }) => ({
        role,
        content: messageContent,
      }),
    );

    this.agentService
      .run(apiMessages)
      .pipe(
        finalize(() => this.isSending.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (response) => this.handleAgentResponse(response),
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(error, 'Failed to run the agent.'),
          );
        },
      });
  }

  private continueRun(approved: boolean): void {
    const runId = this.pendingRunId();
    if (!runId || this.isSending()) {
      return;
    }

    this.errorMessage.set(null);
    this.isSending.set(true);

    this.agentService
      .continue(runId, approved)
      .pipe(
        finalize(() => this.isSending.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (response) => {
          this.pendingRunId.set(null);
          this.pendingPlan.set(null);
          this.handleAgentResponse(response);
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(error, 'Failed to continue the agent run.'),
          );
        },
      });
  }

  private handleAgentResponse(response: AgentResponse): void {
    this.lastPhases.set(response.phases);

    if (response.status === 'AwaitingApproval' && response.runId && response.plan) {
      this.pendingRunId.set(response.runId);
      this.pendingPlan.set(response.plan);
    }

    this.messages.update((current) => [
      ...current,
      {
        role: 'assistant',
        content: response.summary,
        actions: response.actions.length > 0 ? response.actions : undefined,
        phases: response.phases,
        plan: response.plan,
      },
    ]);
  }

  private scrollToBottom(): void {
    const container = this.messagesContainer()?.nativeElement;
    if (!container) {
      return;
    }

    container.scrollTop = container.scrollHeight;
  }
}
