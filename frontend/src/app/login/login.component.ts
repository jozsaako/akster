import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { UserService } from '../services/user.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isSubmitting = signal(false);

  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(6)] }),
  });

  constructor(
    private readonly userService: UserService,
    private readonly router: Router,
  ) {}

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    try {
      const result = await firstValueFrom(this.userService.login(this.form.getRawValue()));

      if (result.success && result.user) {
        this.userService.setTokens(result.token, result.refreshToken);
        this.userService.setUser(result.user);
        await this.router.navigate(['/home']);
      } else {
        this.errorMessage.set(result.message ?? 'Invalid email or password.');
      }
    } catch {
      this.errorMessage.set('Could not reach the server. Please try again.');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
