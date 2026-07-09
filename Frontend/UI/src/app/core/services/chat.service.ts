import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_PATHS } from '../../shared/helpers/api-paths.constants';
import { ChatMessage, ChatRequest, ChatResponse } from '../models';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);

  sendMessage(messages: ChatMessage[]): Observable<ChatResponse> {
    const url = `${environment.apiUrl}${API_PATHS.chat.base}`;
    const request: ChatRequest = { messages };
    return this.http.post<ChatResponse>(url, request);
  }
}
