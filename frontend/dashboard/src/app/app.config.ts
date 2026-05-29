import { ApplicationConfig } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';

// This config enables Angular's HTTP client for API and SignalR-related requests.
export const appConfig: ApplicationConfig = {
  providers: [provideHttpClient()]
};