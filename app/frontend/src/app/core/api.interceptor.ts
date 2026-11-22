import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { URL_API } from './configuracao';
import { MemoriaSessao } from './memoria-sessao';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(URL_API)) {
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
