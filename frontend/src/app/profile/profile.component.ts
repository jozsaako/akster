import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  NgZone,
  OnInit,
  PLATFORM_ID,
  ViewChild,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { UserService } from '../services/user.service';
import { PetService } from '../services/pet.service';
import { AvailabilityService } from '../services/availability.service';
import { Pet, PetGender, PetType } from '../models/user.model';
import { environment } from '../../environments/environment';

declare const google: any;

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileComponent implements OnInit, AfterViewInit {
  @ViewChild('mapContainer') mapContainerRef?: ElementRef<HTMLDivElement>;

  private readonly platformId = inject(PLATFORM_ID);
  private readonly ngZone = inject(NgZone);

  // Profile signals
  protected readonly isSaving = signal(false);
  protected readonly isUploadingAvatar = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly avatarPreview = signal<string | null>(null);
  protected readonly mapReady = signal(false);

  protected readonly form = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    dateOfBirth: new FormControl('', { nonNullable: true }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
  });

  protected readonly today = new Date().toISOString().slice(0, 10);

  // Side menu: sections shown depend on the user's roles
  protected readonly activeSection = signal('info');
  protected readonly sections = computed(() => [
    { id: 'info', label: 'Personal info' },
    { id: 'location', label: 'Location' },
    ...(this.userService.currentUser()?.isOwner ? [{ id: 'pets', label: 'My pets' }] : []),
    ...(this.isSitter() ? [{ id: 'availability', label: 'Sitter profile' }] : []),
  ]);

  protected readonly counties = signal<string[]>([]);
  protected readonly isSavingLocation = signal(false);
  protected readonly postalCodeSuggested = signal(false);
  protected readonly locationError = signal<string | null>(null);
  protected readonly locationSuccess = signal<string | null>(null);
  protected readonly hasLocation = computed(() => this.userService.currentUser()?.location?.latitude != null);

  protected readonly locationForm = new FormGroup({
    county: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    city: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    street: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    postalCode: new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^\d{6}$/)] }),
  });

  // Pet signals
  protected readonly pets = signal<Pet[]>([]);
  protected readonly isPetsLoading = signal(false);
  protected readonly showAddPetForm = signal(false);
  protected readonly editingPetId = signal<number | null>(null);
  protected readonly uploadingPictureForPetId = signal<number | null>(null);
  protected readonly petError = signal<string | null>(null);
  protected readonly petSuccess = signal<string | null>(null);
  protected readonly isSavingPet = signal(false);

  protected readonly petFormGroup = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    age: new FormControl<number>(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    gender: new FormControl<PetGender | ''>('', { nonNullable: true, validators: [Validators.required] }),
    type: new FormControl<PetType | ''>('', { nonNullable: true, validators: [Validators.required] }),
    specialNeeds: new FormControl('', { nonNullable: true }),
  });

  // Availability constants
  protected readonly DAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  protected readonly SLOTS = ['Morning', 'Afternoon', 'Evening'];
  protected readonly SERVICES = [
    { value: 'DogWalking', label: 'Dog walking' },
    { value: 'DropInVisits', label: 'Drop-in visits' },
    { value: 'HomeBoarding', label: 'Home boarding' },
    { value: 'HouseSitting', label: 'House sitting' },
    { value: 'Daycare', label: 'Daycare' },
  ];
  protected readonly PET_TYPES = ['Dog', 'Cat'];

  // Availability signals
  protected readonly isAvailabilityLoading = signal(false);
  protected readonly isSavingAvailability = signal(false);
  protected readonly availabilityError = signal<string | null>(null);
  protected readonly availabilitySuccess = signal<string | null>(null);
  protected readonly schedule = signal<Record<string, string[]>>({});
  protected readonly selectedServices = signal<string[]>([]);
  protected readonly selectedPetTypes = signal<string[]>([]);
  protected readonly maxPets = signal(1);
  protected readonly bio = signal('');
  protected readonly isSitter = signal(false);
  protected readonly isTogglingRole = signal(false);
  protected readonly roleError = signal<string | null>(null);

  private readonly _isOwner = computed(() => !!this.userService.currentUser()?.isOwner);

  private map: any = null;
  private marker: any = null;

  constructor(
    public readonly userService: UserService,
    private readonly petService: PetService,
    private readonly availabilityService: AvailabilityService,
  ) {
    effect(() => {
      const isOwner = this._isOwner();
      untracked(() => {
        if (isOwner) {
          this.pets.set([]);
          this.loadPets();
        } else {
          this.pets.set([]);
        }
      });
    });
    this.loadAvailability();
  }

  ngOnInit(): void {
    firstValueFrom(this.userService.getCounties())
      .then(r => this.counties.set(r.counties))
      .catch(() => this.locationError.set('Could not load the county list.'));

    const user = this.userService.currentUser();
    if (user) {
      this.form.setValue({
        firstName: user.firstName,
        lastName: user.lastName,
        dateOfBirth: user.dateOfBirth ?? '',
        email: user.email,
      });
      this.locationForm.setValue({
        county: user.location?.county ?? '',
        city: user.location?.city ?? '',
        street: user.location?.street ?? '',
        postalCode: user.location?.postalCode ?? '',
      });
    }
  }

  ngAfterViewInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.loadGoogleMaps();
    }
  }

  // --- Side menu ---

  protected scrollTo(id: string, event: Event): void {
    event.preventDefault();
    this.activeSection.set(id);
    document.getElementById('section-' + id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  @HostListener('window:scroll')
  protected onScroll(): void {
    const atBottom = window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 4;
    const ids = this.sections().map(s => s.id);
    const current = atBottom
      ? ids[ids.length - 1]
      : [...ids].reverse().find(id => (document.getElementById('section-' + id)?.getBoundingClientRect().top ?? 1) <= 140);
    this.activeSection.set(current ?? ids[0]);
  }

  // --- Google Maps ---

  private loadGoogleMaps(): void {
    if (typeof google !== 'undefined' && google.maps) {
      this.initMap();
      return;
    }

    const callbackName = '__gmapsCb';
    (window as any)[callbackName] = () => {
      this.ngZone.run(() => this.initMap());
    };

    const script = document.createElement('script');
    script.src = `https://maps.googleapis.com/maps/api/js?key=${environment.googleMapsApiKey}&callback=${callbackName}`;
    script.async = true;
    script.defer = true;
    document.head.appendChild(script);
  }

  private initMap(): void {
    const el = this.mapContainerRef?.nativeElement;
    if (!el) return;

    // Default view: all of Romania
    this.map = new google.maps.Map(el, {
      center: { lat: 45.9432, lng: 24.9668 },
      zoom: 6,
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: false,
    });

    this.marker = new google.maps.Marker({ map: this.map, visible: false });
    this.mapReady.set(true);
    this.showLocationOnMap();
  }

  /** Pins the saved location: the full address when Google can find it, otherwise the stored centroid. */
  private showLocationOnMap(): void {
    const loc = this.userService.currentUser()?.location;
    if (!this.map || loc?.latitude == null || loc.longitude == null) return;

    const centroid = { lat: loc.latitude, lng: loc.longitude };
    const address = [loc.street, loc.city, loc.county, 'Romania'].filter(Boolean).join(', ');
    new google.maps.Geocoder().geocode({ address }, (results: any[], status: string) => {
      this.ngZone.run(() => {
        const position = status === 'OK' && results?.[0] ? results[0].geometry.location : centroid;
        this.marker.setPosition(position);
        this.marker.setVisible(true);
        this.map.setCenter(position);
        this.map.setZoom(status === 'OK' ? 15 : 12);
      });
    });
  }

  // --- Profile methods ---

  protected triggerFileInput(): void {
    const input = document.querySelector<HTMLInputElement>('#avatarFileInput');
    input?.click();
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
      const { firstName, lastName, email, dateOfBirth } = this.form.getRawValue();
      const result = await firstValueFrom(
        this.userService.updateProfile({ firstName, lastName, email, dateOfBirth: dateOfBirth || null })
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

  /** Suggests a postal code from the typed address (browser-side Google geocoding); never overwrites one the user typed. */
  protected suggestPostalCode(): void {
    const { county, city, street, postalCode } = this.locationForm.getRawValue();
    if (postalCode || !county || !city || !street || typeof google === 'undefined' || !google.maps) return;

    const address = [street, city, county, 'Romania'].join(', ');
    new google.maps.Geocoder().geocode({ address, componentRestrictions: { country: 'RO' } }, (results: any[], status: string) => {
      this.ngZone.run(() => {
        const code = status === 'OK'
          ? results?.[0]?.address_components?.find((c: any) => c.types.includes('postal_code'))?.long_name
          : null;
        const control = this.locationForm.controls.postalCode;
        if (code && /^\d{6}$/.test(code) && !control.value) {
          control.setValue(code);
          control.markAsDirty();
          this.postalCodeSuggested.set(true);
        }
      });
    });
  }

  protected async saveLocation(): Promise<void> {
    if (this.locationForm.invalid || this.isSavingLocation()) return;

    this.isSavingLocation.set(true);
    this.locationError.set(null);
    this.locationSuccess.set(null);

    try {
      const { county, city, street, postalCode } = this.locationForm.getRawValue();
      const result = await firstValueFrom(
        this.userService.updateLocation({ county, city, street, postalCode: postalCode || undefined }));

      if (!result.success || !result.user) {
        this.locationError.set(result.message ?? 'Something went wrong.');
        return;
      }

      this.userService.setUser(result.user);
      this.locationForm.markAsPristine();
      this.postalCodeSuggested.set(false);
      this.locationSuccess.set('Location saved.');
      this.showLocationOnMap();
    } catch (e: any) {
      this.locationError.set(e?.error?.message ?? 'Could not reach the server. Please try again.');
    } finally {
      this.isSavingLocation.set(false);
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

  // --- Pet methods ---

  private async loadPets(): Promise<void> {
    this.isPetsLoading.set(true);
    try {
      const result = await firstValueFrom(this.petService.getPets());
      if (result.success) {
        this.pets.set(result.pets ?? []);
      }
    } catch {
      // silently fail on load
    } finally {
      this.isPetsLoading.set(false);
    }
  }

  protected openAddPetForm(): void {
    this.editingPetId.set(null);
    this.petFormGroup.reset({ name: '', age: 0, gender: '', type: '', specialNeeds: '' });
    this.showAddPetForm.set(true);
    this.petError.set(null);
    this.petSuccess.set(null);
  }

  protected openEditPetForm(pet: Pet): void {
    this.showAddPetForm.set(false);
    this.petFormGroup.setValue({
      name: pet.name,
      age: pet.age,
      gender: pet.gender,
      type: pet.type,
      specialNeeds: pet.specialNeeds ?? '',
    });
    this.editingPetId.set(pet.id);
    this.petError.set(null);
    this.petSuccess.set(null);
  }

  protected cancelPetForm(): void {
    this.showAddPetForm.set(false);
    this.editingPetId.set(null);
    this.petFormGroup.reset();
    this.petError.set(null);
    this.petSuccess.set(null);
  }

  protected async savePet(): Promise<void> {
    if (this.petFormGroup.invalid || this.isSavingPet()) return;

    const { name, age, gender, type, specialNeeds } = this.petFormGroup.getRawValue();
    if (!gender || !type) return;

    this.isSavingPet.set(true);
    this.petError.set(null);
    this.petSuccess.set(null);

    try {
      const editId = this.editingPetId();
      const request = { name, age, gender: gender as PetGender, type: type as PetType, specialNeeds: specialNeeds || undefined };

      const result = editId !== null
        ? await firstValueFrom(this.petService.updatePet(editId, request))
        : await firstValueFrom(this.petService.createPet(request));

      if (!result.success) {
        this.petError.set(result.message ?? 'Could not save pet.');
        return;
      }

      if (editId !== null && result.pet) {
        this.pets.update(list => list.map(p => p.id === editId ? result.pet! : p));
        this.petSuccess.set('Pet updated.');
      } else if (result.pet) {
        this.pets.update(list => [...list, result.pet!]);
        this.petSuccess.set('Pet added.');
      }

      this.showAddPetForm.set(false);
      this.editingPetId.set(null);
      this.petFormGroup.reset();
    } catch {
      this.petError.set('Could not reach the server. Please try again.');
    } finally {
      this.isSavingPet.set(false);
    }
  }

  protected async deletePet(petId: number): Promise<void> {
    if (!confirm('Are you sure you want to remove this pet?')) return;

    this.petError.set(null);
    this.petSuccess.set(null);

    try {
      const result = await firstValueFrom(this.petService.deletePet(petId));
      if (!result.success) {
        this.petError.set(result.message ?? 'Could not delete pet.');
        return;
      }
      this.pets.update(list => list.filter(p => p.id !== petId));
      if (this.editingPetId() === petId) {
        this.editingPetId.set(null);
      }
      this.petSuccess.set('Pet removed.');
    } catch {
      this.petError.set('Could not reach the server. Please try again.');
    }
  }

  protected triggerPetPictureInput(petId: number): void {
    const input = document.querySelector<HTMLInputElement>(`#petFileInput-${petId}`);
    input?.click();
  }

  protected async onPetPictureSelected(event: Event, petId: number): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.uploadingPictureForPetId.set(petId);
    this.petError.set(null);
    this.petSuccess.set(null);

    try {
      const result = await firstValueFrom(this.petService.uploadPicture(petId, file));
      if (!result.success) {
        this.petError.set(result.message ?? 'Upload failed.');
        return;
      }
      if (result.pet) {
        this.pets.update(list => list.map(p => p.id === petId ? result.pet! : p));
      }
      this.petSuccess.set('Picture uploaded.');
    } catch {
      this.petError.set('Could not upload picture. Please try again.');
    } finally {
      this.uploadingPictureForPetId.set(null);
      input.value = '';
    }
  }

  protected async deletePetPicture(petId: number, pictureId: number): Promise<void> {
    this.petError.set(null);
    this.petSuccess.set(null);

    try {
      const result = await firstValueFrom(this.petService.deletePicture(petId, pictureId));
      if (!result.success) {
        this.petError.set(result.message ?? 'Could not delete picture.');
        return;
      }
      if (result.pet) {
        this.pets.update(list => list.map(p => p.id === petId ? result.pet! : p));
      }
    } catch {
      this.petError.set('Could not reach the server. Please try again.');
    }
  }

  protected isEditing(petId: number): boolean {
    return this.editingPetId() === petId;
  }

  protected petTypeIcon(type: PetType): string {
    return type === 'Cat' ? '🐱' : '🐶';
  }

  // --- Availability methods ---

  private async loadAvailability(): Promise<void> {
    this.isAvailabilityLoading.set(true);
    try {
      const result = await firstValueFrom(this.availabilityService.getAvailability());
      this.isSitter.set(!!result.availability?.isActive);
      if (result.success && result.availability) {
        this.schedule.set(result.availability.schedule ?? {});
        this.selectedServices.set(result.availability.services ?? []);
        this.selectedPetTypes.set(result.availability.acceptedPetTypes ?? []);
        this.maxPets.set(result.availability.maxPets ?? 1);
        this.bio.set(result.availability.bio ?? '');
      }
    } catch {
      // silently fail on load
    } finally {
      this.isAvailabilityLoading.set(false);
    }
  }

  protected async toggleOwner(event: Event): Promise<void> {
    const checked = (event.target as HTMLInputElement).checked;
    await this.changeRole(async () => {
      const result = await firstValueFrom(this.userService.setOwner(checked));
      if (result.success && result.user) this.userService.setUser(result.user);
      return result.success;
    }, event);
  }

  protected async toggleSitter(event: Event): Promise<void> {
    const checked = (event.target as HTMLInputElement).checked;
    await this.changeRole(async () => {
      const result = await firstValueFrom(
        checked ? this.availabilityService.activate() : this.availabilityService.deactivate());
      if (result.success && result.availability) {
        this.isSitter.set(result.availability.isActive);
        if (checked) await this.loadAvailability();
      }
      return result.success;
    }, event);
  }

  private async changeRole(action: () => Promise<boolean>, event: Event): Promise<void> {
    this.isTogglingRole.set(true);
    this.roleError.set(null);
    try {
      if (!await action()) throw new Error();
    } catch {
      this.roleError.set('Could not update. Please try again.');
      (event.target as HTMLInputElement).checked = !(event.target as HTMLInputElement).checked;
    } finally {
      this.isTogglingRole.set(false);
    }
  }

  protected toggleSlot(day: string, slot: string): void {
    this.schedule.update(s => {
      const current = s[day] ?? [];
      const exists = current.includes(slot);
      return { ...s, [day]: exists ? current.filter(x => x !== slot) : [...current, slot] };
    });
  }

  protected isSlotSelected(day: string, slot: string): boolean {
    return (this.schedule()[day] ?? []).includes(slot);
  }

  protected toggleService(value: string): void {
    this.selectedServices.update(list =>
      list.includes(value) ? list.filter(x => x !== value) : [...list, value]
    );
  }

  protected isServiceSelected(value: string): boolean {
    return this.selectedServices().includes(value);
  }

  protected togglePetType(type: string): void {
    this.selectedPetTypes.update(list =>
      list.includes(type) ? list.filter(x => x !== type) : [...list, type]
    );
  }

  protected isPetTypeSelected(type: string): boolean {
    return this.selectedPetTypes().includes(type);
  }

  protected onMaxPetsChange(event: Event): void {
    const val = parseInt((event.target as HTMLInputElement).value, 10);
    if (!isNaN(val)) this.maxPets.set(Math.min(10, Math.max(1, val)));
  }

  protected onBioChange(event: Event): void {
    this.bio.set((event.target as HTMLTextAreaElement).value);
  }

  protected async saveAvailability(): Promise<void> {
    if (this.isSavingAvailability()) return;

    this.isSavingAvailability.set(true);
    this.availabilityError.set(null);
    this.availabilitySuccess.set(null);

    try {
      const result = await firstValueFrom(this.availabilityService.updateAvailability({
        schedule: this.schedule(),
        services: this.selectedServices(),
        acceptedPetTypes: this.selectedPetTypes(),
        maxPets: this.maxPets(),
        bio: this.bio() || undefined,
      }));

      if (!result.success) {
        this.availabilityError.set(result.message ?? 'Could not save availability.');
        return;
      }

      this.availabilitySuccess.set('Availability saved.');
    } catch {
      this.availabilityError.set('Could not reach the server. Please try again.');
    } finally {
      this.isSavingAvailability.set(false);
    }
  }
}
