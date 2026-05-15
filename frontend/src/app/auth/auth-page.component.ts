import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { NgIf } from '@angular/common';
import { LoginComponent } from '../login/login.component';
import { RegisterComponent } from '../register/register.component';

@Component({
  selector: 'app-auth-page',
  standalone: true,
  imports: [NgIf, LoginComponent, RegisterComponent],
  templateUrl: './auth-page.component.html',
  styleUrls: ['./auth-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthPageComponent {
  protected readonly mode = signal<'login' | 'register'>('login');
  protected readonly isRegister = computed(() => this.mode() === 'register');

  protected setMode(mode: 'login' | 'register'): void {
    this.mode.set(mode);
  }
}
