import { Routes } from '@angular/router';
import { Login } from './features/auth/login/login';
import { Dashboard } from './features/dashboard/dashboard';
import { CanteenSetup } from './features/canteen-setup/canteen-setup';
import { MyCanteens } from './features/canteens/my-canteens/my-canteens';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'dashboard', component: Dashboard, canActivate: [authGuard] },
  { path: 'my-canteens', component: MyCanteens, canActivate: [authGuard] },
  { path: 'canteens', redirectTo: 'my-canteens', pathMatch: 'full' },
  { path: 'canteen-setup', component: CanteenSetup, canActivate: [authGuard] },
  { path: '**', redirectTo: 'login' }
];

