import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_PATHS } from '../../shared/helpers/api-paths.constants';
import {
  AgentContinueRequest,
  AgentMessage,
  AgentRequest,
  AgentResponse,
} from '../models';

@Injectable({ providedIn: 'root' })
export class AgentService {
  private readonly http = inject(HttpClient);

  run(messages: AgentMessage[]): Observable<AgentResponse> {
    const url = `${environment.apiUrl}${API_PATHS.agent.base}`;
    const request: AgentRequest = { messages };
    return this.http.post<AgentResponse>(url, request);
  }

  continue(runId: string, approved: boolean): Observable<AgentResponse> {
    const url = `${environment.apiUrl}${API_PATHS.agent.continue}`;
    const request: AgentContinueRequest = { runId, approved };
    return this.http.post<AgentResponse>(url, request);
  }
}
