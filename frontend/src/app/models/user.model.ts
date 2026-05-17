export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: UserRole;
  profilePictureUrl?: string;
}

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

export interface ChangeRoleRequest {
  Role: UserRole;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  email: string;
}

export interface AuthResult {
  success: boolean;
  user?: User;
  message?: string;
  token: string;
  refreshToken: string;
}

export enum UserRole {
    Owner = 'Owner',
    Sitter = 'Sitter',
}