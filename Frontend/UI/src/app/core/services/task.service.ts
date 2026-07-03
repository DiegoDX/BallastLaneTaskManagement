import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_PATHS } from '../../shared/helpers/api-paths.constants';
import {
  CreateTaskRequest,
  PagedTaskResult,
  TaskItem,
  TaskSearchParams,
  UpdateTaskRequest,
} from '../models';

@Injectable({ providedIn: 'root' })
export class TaskService {
  private readonly http = inject(HttpClient);

  getTasks(params: TaskSearchParams = {}): Observable<PagedTaskResult> {
    const url = `${environment.apiUrl}${API_PATHS.tasks.base}`;
    let httpParams = new HttpParams();

    if (params.pageNumber !== undefined) {
      httpParams = httpParams.set('pageNumber', params.pageNumber);
    }

    if (params.pageSize !== undefined) {
      httpParams = httpParams.set('pageSize', params.pageSize);
    }

    if (params.title) {
      httpParams = httpParams.set('title', params.title);
    }

    if (params.status) {
      httpParams = httpParams.set('status', params.status);
    }

    if (params.sortBy) {
      httpParams = httpParams.set('sortBy', params.sortBy);
    }

    if (params.sortDirection) {
      httpParams = httpParams.set('sortDirection', params.sortDirection);
    }

    return this.http.get<PagedTaskResult>(url, { params: httpParams });
  }

  getTaskById(id: string): Observable<TaskItem> {
    const url = `${environment.apiUrl}${API_PATHS.tasks.byId(id)}`;
    return this.http.get<TaskItem>(url);
  }

  createTask(request: CreateTaskRequest): Observable<TaskItem> {
    const url = `${environment.apiUrl}${API_PATHS.tasks.base}`;
    return this.http.post<TaskItem>(url, request);
  }

  updateTask(id: string, request: UpdateTaskRequest): Observable<void> {
    const url = `${environment.apiUrl}${API_PATHS.tasks.byId(id)}`;
    return this.http.put<void>(url, request);
  }

  deleteTask(id: string): Observable<void> {
    const url = `${environment.apiUrl}${API_PATHS.tasks.byId(id)}`;
    return this.http.delete<void>(url);
  }
}
