import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AvailabilityResult, UpdateAvailabilityRequest } from '../models/user.model';
import { TokenService } from './token.service';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class AvailabilityService {
  private static readonly BASE_URL = `${environment.apiRootUrl}/availability`;

  private readonly tokenService = inject(TokenService);
  private readonly http = inject(HttpClient);

  private get headers(): HttpHeaders | undefined {
    const token = this.tokenService.getToken();
    return token ? new HttpHeaders({ Authorization: `Bearer ${token}` }) : undefined;
  }

  getAvailability(): Observable<AvailabilityResult> {
    return this.http.get<AvailabilityResult>(AvailabilityService.BASE_URL, { headers: this.headers });
  }

  activate(): Observable<AvailabilityResult> {
    return this.http.post<AvailabilityResult>(`${AvailabilityService.BASE_URL}/activate`, {}, { headers: this.headers });
  }

  deactivate(): Observable<AvailabilityResult> {
    return this.http.post<AvailabilityResult>(`${AvailabilityService.BASE_URL}/deactivate`, {}, { headers: this.headers });
  }

  updateAvailability(request: UpdateAvailabilityRequest): Observable<AvailabilityResult> {
    return this.http.put<AvailabilityResult>(AvailabilityService.BASE_URL, request, { headers: this.headers });
  }
}
