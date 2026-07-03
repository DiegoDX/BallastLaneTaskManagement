import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_PATHS } from '../../shared/helpers/api-paths.constants';
import {
  TaskItem,
  TaskSuggestionCreateRequest,
  TaskSuggestionRequest,
  TaskSuggestionResponse,
} from '../models';

@Injectable({ providedIn: 'root' })
export class TaskSuggestionService {
  private readonly http = inject(HttpClient);

  getSuggestionPreview(
    request: TaskSuggestionRequest,
  ): Observable<TaskSuggestionResponse> {
    const url = `${environment.apiUrl}${API_PATHS.tasks.suggestions}`;
    return this.http.post<TaskSuggestionResponse>(url, request);
  }

  createFromSuggestions(
    request: TaskSuggestionCreateRequest,
  ): Observable<TaskItem[]> {
    const url = `${environment.apiUrl}${API_PATHS.tasks.suggestionsCreate}`;
    return this.http.post<TaskItem[]>(url, request);
  }
}
