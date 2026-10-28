import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const adminGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isLoggedIn()) {
    const user = authService.currentUser();
    if (user?.role === 'PlatformAdmin') {
      return true;
    }
    // Authenticated but not a Platform Admin -> redirect to owner dashboard
    return router.createUrlTree(['/dashboard']);
  }

  // Not logged in -> redirect to login with returnUrl
  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url }
  });
};

