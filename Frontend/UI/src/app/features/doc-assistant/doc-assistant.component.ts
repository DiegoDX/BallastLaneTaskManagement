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
import { DocAssistantService } from '../../core/services/doc-assistant.service';
import {
  DocAssistantMessage,
  DocAssistantSource,
} from '../../core/models';
import { resolveHttpErrorMessage } from '../../shared/helpers/http-error.helper';

interface DocAssistantDisplayMessage extends DocAssistantMessage {
  sources?: DocAssistantSource[];
}

@Component({
  selector: 'app-doc-assistant',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './doc-assistant.component.html',
  styleUrl: './doc-assistant.component.css',
})
export class DocAssistantComponent {
  private readonly docAssistantService = inject(DocAssistantService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messagesContainer =
    viewChild<ElementRef<HTMLElement>>('messagesContainer');

  readonly messages = signal<DocAssistantDisplayMessage[]>([]);
  readonly isSending = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly suggestedPrompts = [
    'How does authentication work?',
    'What is the project architecture?',
    'What are the API endpoints?',
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

  formatSourceLabel(source: DocAssistantSource): string {
    return `${source.fileName} (chunk ${source.chunkIndex})`;
  }

  onMessageKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.onSend();
    }
  }

  private sendMessage(content: string): void {
    const userMessage: DocAssistantMessage = { role: 'user', content };
    const updatedMessages = [...this.messages(), userMessage];

    this.messages.set(updatedMessages);
    this.messageControl.reset();
    this.errorMessage.set(null);
    this.isSending.set(true);

    const apiMessages: DocAssistantMessage[] = updatedMessages.map(
      ({ role, content: messageContent }) => ({
        role,
        content: messageContent,
      }),
    );

    this.docAssistantService
      .ask(apiMessages)
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
              sources:
                response.sources.length > 0 ? response.sources : undefined,
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
