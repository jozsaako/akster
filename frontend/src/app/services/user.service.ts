import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthResult, LoginCredentials, RegisterCredentials } from '../models/user.model';

@Injectable({
  providedIn: 'root',
})
export class UserService {

  private static readonly BASE_URL = 'http://localhost:5072/api/auth';
  private static readonly LOGIN_URL = `${UserService.BASE_URL}/login`;
  private static readonly REGISTER_URL = `${UserService.BASE_URL}/register`;
  private static readonly LOGOUT_URL = `${UserService.BASE_URL}/logout`;

  readonly isLoggedIn = signal(false);

  constructor(private readonly http: HttpClient) {}

  login(credentials: LoginCredentials): Observable<AuthResult> {
    return this.http.post<AuthResult>(UserService.LOGIN_URL, credentials);
  }

  register(registration: RegisterCredentials): Observable<AuthResult> {
    return this.http.post<AuthResult>(UserService.REGISTER_URL, registration);
  }

  logout(): Observable<AuthResult> {
    return this.http.post<AuthResult>(UserService.LOGOUT_URL, {});
  }

  setLoggedIn(loggedIn: boolean): void {
    this.isLoggedIn.set(loggedIn);
  }
}
