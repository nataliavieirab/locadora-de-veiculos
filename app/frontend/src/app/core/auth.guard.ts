import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { MemoriaSessao } from './memoria-sessao';

export const authGuard: CanActivateFn = () => {
  if (inject(MemoriaSessao).accessToken()) {
    return true;
  }

  return inject(Router).createUrlTree(['/entrar']);
};
