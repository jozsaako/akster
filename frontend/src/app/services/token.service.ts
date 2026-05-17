import { Injectable, signal, computed } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TokenService {
  private static readonly TOKEN_KEY = 'auth_token';
  private static readonly REFRESH_TOKEN_KEY = 'auth_refresh_token';

  private readonly token = signal<string | null>(this.loadToken());
  private readonly refreshToken = signal<string | null>(this.loadRefreshToken());

  readonly isLoggedIn = computed(() => {
    return this.token() !== null && this.refreshToken() !== null;
  });

  private loadToken(): string | null {
    return localStorage.getItem(TokenService.TOKEN_KEY);
  }

  private loadRefreshToken(): string | null {
    return localStorage.getItem(TokenService.REFRESH_TOKEN_KEY);
  }

  setTokens(token: string, refreshToken: string): void {
    const tokenValue = token === '' ? null : token;
    const refreshTokenValue = refreshToken === '' ? null : refreshToken;

    this.token.set(tokenValue);
    this.refreshToken.set(refreshTokenValue);

    if (tokenValue) {
      localStorage.setItem(TokenService.TOKEN_KEY, tokenValue);
    } else {
      localStorage.removeItem(TokenService.TOKEN_KEY);
    }

    if (refreshTokenValue) {
      localStorage.setItem(TokenService.REFRESH_TOKEN_KEY, refreshTokenValue);
    } else {
      localStorage.removeItem(TokenService.REFRESH_TOKEN_KEY);
    }
  }

  clearTokens(): void {
    this.token.set(null);
    this.refreshToken.set(null);
    localStorage.removeItem(TokenService.TOKEN_KEY);
    localStorage.removeItem(TokenService.REFRESH_TOKEN_KEY);
  }

  getToken(): string | null {
    return this.token();
  }

  getRefreshToken(): string | null {
    return this.refreshToken();
  }
}
