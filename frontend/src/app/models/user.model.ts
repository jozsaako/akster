export interface LoginCredentials {
  email: string;
  password: string;
}

export interface RegisterCredentials {
    email: string;
    password: string;
    firstName?: string;
    lastName?: string;
}

export interface AuthResult {
  success: boolean;
  user?: {
    displayName?: string;
    email: string;
  };
  message?: string;
}