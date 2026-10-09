export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  dateOfBirth?: string | null;
  isOwner: boolean;
  profilePictureUrl?: string;
  location?: UserLocation | null;
}

/** The user's own location. latitude/longitude are null for a legacy free-text address (street only). */
export interface UserLocation {
  county: string | null;
  city: string | null;
  postalCode: string | null;
  street: string | null;
  latitude: number | null;
  longitude: number | null;
}

export interface UpdateLocationRequest {
  county: string;
  city: string;
  street: string;
  postalCode?: string;
}

export interface CountiesResult {
  success: boolean;
  counties: string[];
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

export interface SetOwnerRequest {
  isOwner: boolean;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  email: string;
  dateOfBirth?: string | null;
}

export interface AuthResult {
  success: boolean;
  user?: User;
  message?: string;
  token: string;
  refreshToken: string;
}

export type PetType = 'Cat' | 'Dog';
export type PetGender = 'Male' | 'Female';

export interface PetPicture {
  id: number;
  url: string;
  uploadedAt: string;
}

export interface Pet {
  id: number;
  userId: number;
  name: string;
  age: number;
  gender: PetGender;
  type: PetType;
  specialNeeds?: string;
  createdAt: string;
  updatedAt: string;
  pictures: PetPicture[];
}

export interface CreatePetRequest {
  name: string;
  age: number;
  gender: PetGender;
  type: PetType;
  specialNeeds?: string;
}

export interface UpdatePetRequest {
  name: string;
  age: number;
  gender: PetGender;
  type: PetType;
  specialNeeds?: string;
}

export interface PetResult {
  success: boolean;
  message?: string;
  pet?: Pet;
  pets?: Pet[];
}

export interface SitterAvailability {
  id: number;
  userId: number;
  isActive: boolean;
  schedule: Record<string, string[]>;
  services: string[];
  acceptedPetTypes: string[];
  maxPets: number;
  bio: string | null;
  updatedAt: string;
}

export interface UpdateAvailabilityRequest {
  schedule: Record<string, string[]>;
  services: string[];
  acceptedPetTypes: string[];
  maxPets: number;
  bio?: string;
}

export interface AvailabilityResult {
  success: boolean;
  message?: string;
  availability?: SitterAvailability;
}