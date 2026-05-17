import { ChangeDetectionStrategy, Component, HostListener, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { UserRole } from '../models/user.model';
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
  protected readonly isSwitchingRole = signal(false);
  protected readonly dropdownOpen = signal(false);

  constructor(
    public readonly userService: UserService,
    private readonly router: Router,
  ) {}

  protected get oppositeRole(): UserRole {
    return this.userService.currentUser()?.role === UserRole.Owner
      ? UserRole.Sitter
      : UserRole.Owner;
  }

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

  protected async switchRole(): Promise<void> {
    const newRole = this.oppositeRole;
    this.isSwitchingRole.set(true);
    try {
      const result = await firstValueFrom(this.userService.changeRole(newRole));
      if (result.user) {
        this.userService.setUser(result.user);
      } else {
        const current = this.userService.currentUser();
        if (current) this.userService.setUser({ ...current, role: newRole });
      }
      if (result.token && result.refreshToken) {
        this.userService.setTokens(result.token, result.refreshToken);
      }
    } finally {
      this.isSwitchingRole.set(false);
    }
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
