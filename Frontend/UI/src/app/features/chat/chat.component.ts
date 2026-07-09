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
import { ChatService } from '../../core/services/chat.service';
import { ChatMessage } from '../../core/models';
import { resolveHttpErrorMessage } from '../../shared/helpers/http-error.helper';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.css',
})
export class ChatComponent {
  private readonly chatService = inject(ChatService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messagesContainer =
    viewChild<ElementRef<HTMLElement>>('messagesContainer');

  readonly messages = signal<ChatMessage[]>([]);
  readonly isSending = signal(false);
  readonly errorMessage = signal<string | null>(null);

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

    const userMessage: ChatMessage = { role: 'user', content };
    const updatedMessages = [...this.messages(), userMessage];

    this.messages.set(updatedMessages);
    this.messageControl.reset();
    this.errorMessage.set(null);
    this.isSending.set(true);

    this.chatService
      .sendMessage(updatedMessages)
      .pipe(
        finalize(() => this.isSending.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (response) => {
          this.messages.update((current) => [
            ...current,
            { role: 'assistant', content: response.content },
          ]);
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            resolveHttpErrorMessage(error, 'Failed to send message.'),
          );
        },
      });
  }

  onMessageKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.onSend();
    }
  }

  private scrollToBottom(): void {
    const container = this.messagesContainer()?.nativeElement;
    if (!container) {
      return;
    }

    container.scrollTop = container.scrollHeight;
  }
}
