import { Injectable, signal, effect } from '@angular/core';

export type ThemeMode = 'light' | 'dark' | 'system';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly storageKey = 'canteen_theme_preference';
  
  // Current user selection: 'light' | 'dark' | 'system'. Default: 'light'
  readonly currentMode = signal<ThemeMode>(this.getSavedMode());
  
  // Effective active theme: 'light' | 'dark'
  readonly activeTheme = signal<'light' | 'dark'>('light');

  constructor() {
    this.applyTheme(this.currentMode());

    // Listen to OS color scheme changes if in 'system' mode
    if (typeof window !== 'undefined' && window.matchMedia) {
      const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      mediaQuery.addEventListener('change', () => {
        if (this.currentMode() === 'system') {
          this.applyTheme('system');
        }
      });
    }
  }

  setMode(mode: ThemeMode): void {
    this.currentMode.set(mode);
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(this.storageKey, mode);
    }
    this.applyTheme(mode);
  }

  toggleTheme(): void {
    const current = this.activeTheme();
    this.setMode(current === 'light' ? 'dark' : 'light');
  }

  private getSavedMode(): ThemeMode {
    if (typeof localStorage !== 'undefined') {
      const saved = localStorage.getItem(this.storageKey) as ThemeMode | null;
      if (saved === 'light' || saved === 'dark' || saved === 'system') {
        return saved;
      }
    }
    return 'light'; // Light mode is default for first-time users
  }

  private applyTheme(mode: ThemeMode): void {
    let resolvedTheme: 'light' | 'dark' = 'light';

    if (mode === 'system') {
      if (typeof window !== 'undefined' && window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
        resolvedTheme = 'dark';
      } else {
        resolvedTheme = 'light';
      }
    } else {
      resolvedTheme = mode;
    }

    this.activeTheme.set(resolvedTheme);

    if (typeof document !== 'undefined') {
      const root = document.documentElement;
      root.setAttribute('data-theme', resolvedTheme);
      // Also sync standard color-scheme for native inputs
      root.style.colorScheme = resolvedTheme;
    }
  }
}
