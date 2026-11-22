import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'painel' },
  {
    path: 'entrar',
    loadComponent: () => import('./features/acesso/login.component').then((modulo) => modulo.LoginComponent),
  },
  {
    path: 'registrar',
    loadComponent: () => import('./features/acesso/registro.component').then((modulo) => modulo.RegistroComponent),
  },
  {
    path: 'painel',
    canActivate: [authGuard],
    loadComponent: () => import('./features/acesso/painel.component').then((modulo) => modulo.PainelComponent),
  },
];
