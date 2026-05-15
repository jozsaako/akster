import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { UserService } from '../services/user.service';

@Component({
  selector: 'app-home',
  standalone: true,
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent {
  constructor(
    private readonly userService: UserService,
    private readonly router: Router
  ) {}

  protected async logout(): Promise<void> {
    try {
      await firstValueFrom(this.userService.logout());
    } finally {
      this.userService.setLoggedIn(false);
      await this.router.navigate(['']);
    }
  }
}
