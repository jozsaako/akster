import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { LoginComponent } from '../login/login.component';
import { RegisterComponent } from '../register/register.component';

@Component({
  selector: 'app-auth-page',
  standalone: true,
  imports: [LoginComponent, RegisterComponent],
  templateUrl: './auth-page.component.html',
  styleUrls: ['./auth-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthPageComponent {
  protected readonly mode = signal<'login' | 'register'>('login');

  protected setMode(mode: 'login' | 'register'): void {
    this.mode.set(mode);
  }
}
