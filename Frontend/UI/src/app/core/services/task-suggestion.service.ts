import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_PATHS } from '../../shared/helpers/api-paths.constants';
import {
  TaskItem,
  TaskSuggestionBatchResponse,
  TaskSuggestionCreateRequest,
  TaskSuggestionRequest,
} from '../models';

@Injectable({ providedIn: 'root' })
export class TaskSuggestionService {
  private readonly http = inject(HttpClient);

  generateBatch(
    request: TaskSuggestionRequest,
  ): Observable<TaskSuggestionBatchResponse> {
    const url = `${environment.apiUrl}${API_PATHS.tasks.suggestionsGenerate}`;
    return this.http.post<TaskSuggestionBatchResponse>(url, request);
  }

  createFromSuggestions(
    request: TaskSuggestionCreateRequest,
  ): Observable<TaskItem[]> {
    const url = `${environment.apiUrl}${API_PATHS.tasks.suggestionsCreate}`;
    return this.http.post<TaskItem[]>(url, request);
  }
}
