import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
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
  @ViewChild('addressInput') addressInputRef?: ElementRef<HTMLInputElement>;

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
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    address: new FormControl('', { nonNullable: true }),
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
  private autocomplete: any = null;

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
    const user = this.userService.currentUser();
    if (user) {
      this.form.setValue({
        firstName: user.firstName,
        lastName: user.lastName,
        email: user.email,
        address: user.address ?? '',
      });
    }
  }

  ngAfterViewInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.loadGoogleMaps();
    }
  }

  // --- Google Maps ---

  private loadGoogleMaps(): void {
    if (typeof google !== 'undefined' && google.maps) {
      this.initMap();
      this.initAutocomplete();
      return;
    }

    const callbackName = '__gmapsCb';
    (window as any)[callbackName] = () => {
      this.ngZone.run(() => {
        this.initMap();
        this.initAutocomplete();
      });
    };

    const script = document.createElement('script');
    script.src = `https://maps.googleapis.com/maps/api/js?key=${environment.googleMapsApiKey}&libraries=places&callback=${callbackName}`;
    script.async = true;
    script.defer = true;
    document.head.appendChild(script);
  }

  private initMap(): void {
    const el = this.mapContainerRef?.nativeElement;
    if (!el) return;

    // Default center: Budapest
    const defaultCenter = { lat: 47.4979, lng: 19.0402 };

    this.map = new google.maps.Map(el, {
      center: defaultCenter,
      zoom: 11,
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: false,
    });

    this.marker = new google.maps.Marker({
      map: this.map,
      draggable: true,
      visible: false,
    });

    // Allow dragging the marker to update address
    this.marker.addListener('dragend', (event: any) => {
      this.ngZone.run(() => this.reverseGeocode(event.latLng));
    });

    // Click map to drop pin
    this.map.addListener('click', (event: any) => {
      this.ngZone.run(() => {
        this.placeMarker(event.latLng);
        this.reverseGeocode(event.latLng);
      });
    });

    this.mapReady.set(true);

    // If user already has an address, geocode it on load
    const existing = this.form.controls.address.value;
    if (existing) {
      this.geocodeAddress(existing);
    }
  }

  private initAutocomplete(): void {
    const input = this.addressInputRef?.nativeElement;
    if (!input) return;

    this.autocomplete = new google.maps.places.Autocomplete(input, {
      fields: ['formatted_address', 'geometry'],
    });

    this.autocomplete.addListener('place_changed', () => {
      this.ngZone.run(() => {
        const place = this.autocomplete.getPlace();
        if (!place?.geometry) return;

        this.form.controls.address.setValue(place.formatted_address ?? '');
        this.form.controls.address.markAsDirty();

        const location = place.geometry.location;
        this.placeMarker(location);
        this.map?.setCenter(location);
        this.map?.setZoom(15);
      });
    });
  }

  private placeMarker(latLng: any): void {
    if (!this.marker) return;
    this.marker.setPosition(latLng);
    this.marker.setVisible(true);
  }

  private reverseGeocode(latLng: any): void {
    const geocoder = new google.maps.Geocoder();
    geocoder.geocode({ location: latLng }, (results: any[], status: string) => {
      this.ngZone.run(() => {
        if (status === 'OK' && results?.[0]) {
          this.form.controls.address.setValue(results[0].formatted_address);
          this.form.controls.address.markAsDirty();
        }
      });
    });
  }

  private geocodeAddress(address: string): void {
    const geocoder = new google.maps.Geocoder();
    geocoder.geocode({ address }, (results: any[], status: string) => {
      this.ngZone.run(() => {
        if (status === 'OK' && results?.[0]) {
          const location = results[0].geometry.location;
          this.placeMarker(location);
          this.map?.setCenter(location);
          this.map?.setZoom(15);
        }
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
      const { firstName, lastName, email, address } = this.form.getRawValue();
      const result = await firstValueFrom(
        this.userService.updateProfile({ firstName, lastName, email, address: address || undefined })
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
