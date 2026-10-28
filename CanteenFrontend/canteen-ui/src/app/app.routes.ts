import { Routes } from '@angular/router';
import { Login } from './features/auth/login/login';
import { Dashboard } from './features/dashboard/dashboard';
import { CanteenSetup } from './features/canteen-setup/canteen-setup';
import { MyCanteens } from './features/canteens/my-canteens/my-canteens';
import { CustomerMenu } from './features/customer-menu/customer-menu';
import { FoodItems } from './features/food-items/food-items';
import { Categories } from './features/categories/categories';
import { AdminDashboard } from './features/admin/admin-dashboard/admin-dashboard';
import { AdminCanteens } from './features/admin/admin-canteens/admin-canteens';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'menu', component: CustomerMenu },
  
  // Platform Admin Routes (Protected by adminGuard)
  { path: 'admin', redirectTo: 'admin/dashboard', pathMatch: 'full' },
  { path: 'admin/dashboard', component: AdminDashboard, canActivate: [adminGuard] },
  { path: 'admin/canteens', component: AdminCanteens, canActivate: [adminGuard] },

  // Canteen Owner Routes (Protected by authGuard)
  { path: 'dashboard', component: Dashboard, canActivate: [authGuard] },
  { path: 'food-items', component: FoodItems, canActivate: [authGuard] },
  { path: 'dishes', redirectTo: 'food-items', pathMatch: 'full' },
  { path: 'menu-items', redirectTo: 'food-items', pathMatch: 'full' },
  { path: 'categories', component: Categories, canActivate: [authGuard] },
  { path: 'my-canteens', component: MyCanteens, canActivate: [authGuard] },
  { path: 'canteens', redirectTo: 'my-canteens', pathMatch: 'full' },
  { path: 'canteen-setup', component: CanteenSetup, canActivate: [authGuard] },
  
  { path: '**', redirectTo: 'login' }
];
