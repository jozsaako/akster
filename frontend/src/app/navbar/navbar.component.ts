import { ChangeDetectionStrategy, Component, HostListener, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { UserService } from '../services/user.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './navbar.component.html',
  styleUrls: ['./navbar.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NavbarComponent {
  protected readonly dropdownOpen = signal(false);

  constructor(
    public readonly userService: UserService,
    private readonly router: Router,
  ) {}

  protected get initials(): string {
    const user = this.userService.currentUser();
    return ((user?.firstName?.[0] ?? '') + (user?.lastName?.[0] ?? '')).toUpperCase();
  }

  protected toggleDropdown(event: MouseEvent): void {
    event.stopPropagation();
    this.dropdownOpen.update(v => !v);
  }

  @HostListener('document:click')
  protected closeDropdown(): void {
    this.dropdownOpen.set(false);
  }

  protected async logout(): Promise<void> {
    this.dropdownOpen.set(false);
    try {
      await firstValueFrom(this.userService.logout());
    } finally {
      this.userService.clearTokens();
      await this.router.navigate(['login']);
    }
  }
}
