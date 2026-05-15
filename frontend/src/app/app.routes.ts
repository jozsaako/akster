import { Routes } from '@angular/router';
import { AuthPageComponent } from './auth/auth-page.component';
import { HomeComponent } from './home/home.component';
import { authGuard, noAuthGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', component: AuthPageComponent, canActivate: [noAuthGuard] },
  { path: 'home', component: HomeComponent, canActivate: [authGuard] },
];
