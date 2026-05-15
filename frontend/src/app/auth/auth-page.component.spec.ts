import { TestBed } from '@angular/core/testing';
import { AuthPageComponent } from './auth-page.component';

describe('AuthPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AuthPageComponent],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(AuthPageComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
