import { ChangeDetectionStrategy, Component, ElementRef, OnInit, ViewChild, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { UserService } from '../services/user.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileComponent implements OnInit {
  @ViewChild('fileInput') private fileInput!: ElementRef<HTMLInputElement>;

  protected readonly isSaving = signal(false);
  protected readonly isUploadingAvatar = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly avatarPreview = signal<string | null>(null);

  protected readonly form = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
  });

  constructor(public readonly userService: UserService) {}

  ngOnInit(): void {
    const user = this.userService.currentUser();
    if (user) {
      this.form.setValue({
        firstName: user.firstName,
        lastName: user.lastName,
        email: user.email,
      });
    }
  }

  protected triggerFileInput(): void {
    this.fileInput.nativeElement.click();
  }

  protected async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = () => this.avatarPreview.set(reader.result as string);
    reader.readAsDataURL(file);

    this.isUploadingAvatar.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const result = await firstValueFrom(this.userService.uploadAvatar(file));
      if (!result.success) {
        this.errorMessage.set(result.message ?? 'Upload failed.');
        this.avatarPreview.set(null);
        return;
      }
      if (result.user) {
        this.userService.setUser(result.user);
      }
      this.successMessage.set('Profile picture updated.');
    } catch {
      this.errorMessage.set('Could not upload image. Please try again.');
      this.avatarPreview.set(null);
    } finally {
      this.isUploadingAvatar.set(false);
      input.value = '';
    }
  }

  protected async save(): Promise<void> {
    if (this.form.invalid || this.isSaving()) return;

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const result = await firstValueFrom(
        this.userService.updateProfile(this.form.getRawValue())
      );

      if (!result.success) {
        this.errorMessage.set(result.message ?? 'Something went wrong.');
        return;
      }

      if (result.user) {
        this.userService.setUser(result.user);
      }
      if (result.token) {
        this.userService.setTokens(result.token, this.userService.getRefreshToken() ?? '');
      }

      this.form.markAsPristine();
      this.successMessage.set('Profile saved.');
    } catch {
      this.errorMessage.set('Could not reach the server. Please try again.');
    } finally {
      this.isSaving.set(false);
    }
  }

  protected get avatarUrl(): string | null {
    return this.avatarPreview() ?? this.userService.currentUser()?.profilePictureUrl ?? null;
  }

  protected get initials(): string {
    const first = (this.form.value.firstName?.[0] ?? '').toUpperCase();
    const last = (this.form.value.lastName?.[0] ?? '').toUpperCase();
    return first + last;
  }
}
