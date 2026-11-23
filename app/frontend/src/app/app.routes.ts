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
    path: 'grupos-planos',
    canActivate: [authGuard],
    loadComponent: () => import('./features/frota/grupos-planos.component').then((modulo) => modulo.GruposPlanosComponent),
  },
  {
    path: 'veiculos',
    canActivate: [authGuard],
    loadComponent: () => import('./features/frota/veiculos.component').then((modulo) => modulo.VeiculosComponent),
  },
  {
    path: 'clientes',
    canActivate: [authGuard],
    loadComponent: () => import('./features/pessoas/pessoas.component').then((modulo) => modulo.PessoasComponent),
  },
  {
    path: 'precos-servicos',
    canActivate: [authGuard],
    loadComponent: () => import('./features/precos/precos.component').then((modulo) => modulo.PrecosComponent),
  },
  {
    path: 'painel',
    canActivate: [authGuard],
    loadComponent: () => import('./features/acesso/painel.component').then((modulo) => modulo.PainelComponent),
  },
];
