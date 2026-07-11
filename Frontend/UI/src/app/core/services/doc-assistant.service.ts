import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_PATHS } from '../../shared/helpers/api-paths.constants';
import {
  DocAssistantMessage,
  DocAssistantRequest,
  DocAssistantResponse,
} from '../models';

@Injectable({ providedIn: 'root' })
export class DocAssistantService {
  private readonly http = inject(HttpClient);

  ask(messages: DocAssistantMessage[]): Observable<DocAssistantResponse> {
    const url = `${environment.apiUrl}${API_PATHS.docAssistant.base}`;
    const request: DocAssistantRequest = { messages };
    return this.http.post<DocAssistantResponse>(url, request);
  }
}
