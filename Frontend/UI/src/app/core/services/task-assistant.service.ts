import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_PATHS } from '../../shared/helpers/api-paths.constants';
import {
  TaskAssistantMessage,
  TaskAssistantRequest,
  TaskAssistantResponse,
} from '../models';

@Injectable({ providedIn: 'root' })
export class TaskAssistantService {
  private readonly http = inject(HttpClient);

  assist(messages: TaskAssistantMessage[]): Observable<TaskAssistantResponse> {
    const url = `${environment.apiUrl}${API_PATHS.taskAssistant.base}`;
    const request: TaskAssistantRequest = { messages };
    return this.http.post<TaskAssistantResponse>(url, request);
  }
}
