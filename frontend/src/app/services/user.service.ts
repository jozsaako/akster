import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthResult, ChangeRoleRequest, LoginCredentials, RegisterCredentials, UpdateProfileRequest, User, UserRole } from '../models/user.model';
import { TokenService } from './token.service';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class UserService {

  private static readonly BASE_URL = environment.apiBaseUrl;
  private static readonly LOGIN_URL = `${UserService.BASE_URL}/login`;
  private static readonly REGISTER_URL = `${UserService.BASE_URL}/register`;
  private static readonly CHANGE_ROLE_URL = `${UserService.BASE_URL}/change-role`;
  private static readonly LOGOUT_URL = `${UserService.BASE_URL}/logout`;
  private static readonly ME_URL = `${UserService.BASE_URL}/me`;
  private static readonly AVATAR_URL = `${UserService.BASE_URL}/me/avatar`;
  private static readonly USER_KEY = 'current_user';

  private readonly tokenService = inject(TokenService);
  private readonly http = inject(HttpClient);

  readonly isLoggedIn = this.tokenService.isLoggedIn;
  readonly currentUser = signal<User | null>(this.loadUser());

  private loadUser(): User | null {
    const userJson = localStorage.getItem(UserService.USER_KEY);
    return userJson ? JSON.parse(userJson) : null;
  }

  login(credentials: LoginCredentials): Observable<AuthResult> {
    return this.http.post<AuthResult>(UserService.LOGIN_URL, credentials);
  }

  register(registration: RegisterCredentials): Observable<AuthResult> {
    return this.http.post<AuthResult>(UserService.REGISTER_URL, registration);
  }

  updateProfile(data: UpdateProfileRequest): Observable<AuthResult> {
    const token = this.tokenService.getToken();
    const headers = token
      ? new HttpHeaders({ Authorization: `Bearer ${token}` })
      : undefined;
    return this.http.patch<AuthResult>(UserService.ME_URL, data, { headers });
  }

  getMe(): Observable<AuthResult> {
    const token = this.tokenService.getToken();
    const headers = token
      ? new HttpHeaders({ Authorization: `Bearer ${token}` })
      : undefined;
    return this.http.get<AuthResult>(UserService.ME_URL, { headers });
  }

  changeRole(role: UserRole): Observable<AuthResult> {
    const request: ChangeRoleRequest = { Role: role };
    const token = this.tokenService.getToken();
    const headers = token
      ? new HttpHeaders({ Authorization: `Bearer ${token}` })
      : undefined;

    return this.http.post<AuthResult>(UserService.CHANGE_ROLE_URL, request, { headers });
  }

  uploadAvatar(file: File): Observable<AuthResult> {
    const token = this.tokenService.getToken();
    const headers = token
      ? new HttpHeaders({ Authorization: `Bearer ${token}` })
      : undefined;
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<AuthResult>(UserService.AVATAR_URL, formData, { headers });
  }

  logout(): Observable<AuthResult> {
    return this.http.post<AuthResult>(UserService.LOGOUT_URL, {});
  }

  setUser(user: User): void {
    this.currentUser.set(user);
    localStorage.setItem(UserService.USER_KEY, JSON.stringify(user));
  }

  clearUser(): void {
    this.currentUser.set(null);
    localStorage.removeItem(UserService.USER_KEY);
  }

  setTokens(token: string, refreshToken: string): void {
    this.tokenService.setTokens(token, refreshToken);
  }

  clearTokens(): void {
    this.clearUser();
    this.tokenService.clearTokens();
  }

  getToken(): string | null {
    return this.tokenService.getToken();
  }

  getRefreshToken(): string | null {
    return this.tokenService.getRefreshToken();
  }
}
