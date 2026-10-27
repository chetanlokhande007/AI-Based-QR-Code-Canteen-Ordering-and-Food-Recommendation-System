import { Component, ChangeDetectorRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

interface StatCard {
  title: string;
  value: string;
  trend: string;
  isPositive: boolean;
  icon: string;
}

interface RecentOrder {
  id: string;
  table: string;
  items: string;
  amount: string;
  status: 'Preparing' | 'Ready' | 'Completed';
  timeAgo: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class Dashboard {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly currentUser = this.authService.currentUser;

  // Real-time current date display
  readonly currentDate = new Date().toLocaleDateString('en-US', {
    weekday: 'long',
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  });

  // KPI Metrics
  readonly stats = signal<StatCard[]>([
    {
      title: "Today's Orders",
      value: '142',
      trend: '+18.4% from yesterday',
      isPositive: true,
      icon: 'orders'
    },
    {
      title: 'Daily Revenue',
      value: '₹18,450',
      trend: '+12.6% vs avg weekday',
      isPositive: true,
      icon: 'revenue'
    },
    {
      title: 'Avg Kitchen Prep Time',
      value: '8.5 min',
      trend: '-1.2 min faster',
      isPositive: true,
      icon: 'speed'
    },
    {
      title: 'AI Recommendation Uplift',
      value: '34.2%',
      trend: '+4.5% conversion',
      isPositive: true,
      icon: 'ai'
    }
  ]);

  // Live order stream preview
  readonly recentOrders = signal<RecentOrder[]>([
    {
      id: '#ORD-1082',
      table: 'Table 4',
      items: 'Paneer Butter Masala, 2x Naan, Mango Lassi',
      amount: '₹420',
      status: 'Preparing',
      timeAgo: '2m ago'
    },
    {
      id: '#ORD-1081',
      table: 'Table 12',
      items: 'Crispy Veg Burger, Peri-Peri Fries, Cold Coffee',
      amount: '₹310',
      status: 'Ready',
      timeAgo: '6m ago'
    },
    {
      id: '#ORD-1080',
      table: 'Table 7',
      items: 'Masala Dosa, Filter Coffee',
      amount: '₹160',
      status: 'Completed',
      timeAgo: '14m ago'
    },
    {
      id: '#ORD-1079',
      table: 'Takeaway #18',
      items: 'Chicken Biryani, Raita, Gulab Jamun',
      amount: '₹380',
      status: 'Completed',
      timeAgo: '22m ago'
    }
  ]);

  get ownerInitials(): string {
    const name = this.currentUser()?.fullName?.trim();
    if (!name) return 'CO';
    const parts = name.split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
