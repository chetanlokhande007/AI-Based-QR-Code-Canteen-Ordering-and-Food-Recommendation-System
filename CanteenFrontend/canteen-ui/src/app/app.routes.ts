import { Routes } from '@angular/router';
import { Login } from './features/auth/login/login';
import { Dashboard } from './features/dashboard/dashboard';
import { CanteenSetup } from './features/canteen-setup/canteen-setup';
import { MyCanteens } from './features/canteens/my-canteens/my-canteens';
import { CustomerMenu } from './features/customer-menu/customer-menu';
import { FoodItems } from './features/food-items/food-items';
import { Categories } from './features/categories/categories';
import { TablesComponent } from './features/tables/tables';
import { AdminDashboard } from './features/admin/admin-dashboard/admin-dashboard';
import { AdminCanteens } from './features/admin/admin-canteens/admin-canteens';
import { AdminCanteenDetailsComponent } from './features/admin/admin-canteen-details/admin-canteen-details';
import { AdminTables } from './features/admin/admin-tables/admin-tables';
import { AdminOwnersComponent } from './features/admin/admin-owners/admin-owners';
import { AdminPaymentsComponent } from './features/admin/admin-payments/admin-payments';
import { AdminAuditLogsComponent } from './features/admin/admin-audit-logs/admin-audit-logs';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'canteen/login', redirectTo: 'login', pathMatch: 'full' },
  { path: 'canteen/register', component: Login },
  { path: 'menu', component: CustomerMenu },
  { path: 'c/:slug', component: CustomerMenu },
  
  // Platform Admin Routes (Protected by adminGuard)
  { path: 'admin', redirectTo: 'admin/dashboard', pathMatch: 'full' },
  { path: 'admin/dashboard', component: AdminDashboard, canActivate: [adminGuard] },
  { path: 'admin/canteens', component: AdminCanteens, canActivate: [adminGuard] },
  { path: 'admin/canteens/:id', component: AdminCanteenDetailsComponent, canActivate: [adminGuard] },
  { path: 'admin/owners', component: AdminOwnersComponent, canActivate: [adminGuard] },
  { path: 'admin/tables', component: AdminTables, canActivate: [adminGuard] },
  { path: 'admin/payments', component: AdminPaymentsComponent, canActivate: [adminGuard] },
  { path: 'admin/audit-logs', component: AdminAuditLogsComponent, canActivate: [adminGuard] },

  // Canteen Owner Routes (Protected by authGuard)
  { path: 'dashboard', component: Dashboard, canActivate: [authGuard] },
  { path: 'owner/dashboard', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'food-items', component: FoodItems, canActivate: [authGuard] },
  { path: 'dishes', redirectTo: 'food-items', pathMatch: 'full' },
  { path: 'menu-items', redirectTo: 'food-items', pathMatch: 'full' },
  { path: 'categories', component: Categories, canActivate: [authGuard] },
  { path: 'tables', component: TablesComponent, canActivate: [authGuard] },
  { path: 'tables-qr', redirectTo: 'tables', pathMatch: 'full' },
  { path: 'my-canteens', component: MyCanteens, canActivate: [authGuard] },
  { path: 'canteens', redirectTo: 'my-canteens', pathMatch: 'full' },
  { path: 'canteen-setup', component: CanteenSetup, canActivate: [authGuard] },
  { path: 'canteen/setup', redirectTo: 'canteen-setup', pathMatch: 'full' },
  
  { path: '**', redirectTo: 'login' }
];
