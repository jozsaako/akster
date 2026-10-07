import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreatePetRequest, PetResult, UpdatePetRequest } from '../models/user.model';
import { TokenService } from './token.service';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class PetService {
  private static readonly BASE_URL = `${environment.apiRootUrl}/pets`;

  private readonly tokenService = inject(TokenService);
  private readonly http = inject(HttpClient);

  private get headers(): HttpHeaders | undefined {
    const token = this.tokenService.getToken();
    return token ? new HttpHeaders({ Authorization: `Bearer ${token}` }) : undefined;
  }

  getPets(): Observable<PetResult> {
    return this.http.get<PetResult>(PetService.BASE_URL, { headers: this.headers });
  }

  createPet(request: CreatePetRequest): Observable<PetResult> {
    return this.http.post<PetResult>(PetService.BASE_URL, request, { headers: this.headers });
  }

  updatePet(petId: number, request: UpdatePetRequest): Observable<PetResult> {
    return this.http.put<PetResult>(`${PetService.BASE_URL}/${petId}`, request, { headers: this.headers });
  }

  deletePet(petId: number): Observable<PetResult> {
    return this.http.delete<PetResult>(`${PetService.BASE_URL}/${petId}`, { headers: this.headers });
  }

  uploadPicture(petId: number, file: File): Observable<PetResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<PetResult>(`${PetService.BASE_URL}/${petId}/pictures`, formData, { headers: this.headers });
  }

  deletePicture(petId: number, pictureId: number): Observable<PetResult> {
    return this.http.delete<PetResult>(`${PetService.BASE_URL}/${petId}/pictures/${pictureId}`, { headers: this.headers });
  }
}
