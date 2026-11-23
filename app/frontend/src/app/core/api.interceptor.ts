import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { URL_API } from './configuracao';
import { MemoriaSessao } from './memoria-sessao';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  let destino: URL;
  try {
    destino = new URL(req.url, globalThis.location?.origin ?? URL_API);
  } catch {
    return next(req);
  }
  if (destino.origin !== new URL(URL_API).origin || !destino.pathname.startsWith('/api/v1/')) {
    return next(req);
  }

  const token = inject(MemoriaSessao).accessToken();
  if (!token) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
};
