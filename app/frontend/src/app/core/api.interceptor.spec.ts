import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { apiInterceptor } from './api.interceptor';
import { authGuard } from './auth.guard';
import { MemoriaSessao } from './memoria-sessao';
import { URL_API } from './configuracao';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { routes } from '../app.routes';

describe('Isolamento de sessão no frontend', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiInterceptor])), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(MemoriaSessao).accessToken.set('token-apenas-memoria');
  });
  afterEach(() => http.verify());

  it.each([
    `${URL_API}/api/v1/clientes`, `${URL_API}/api/v1/configuracoes/seguros/Cliente`,
  ])('anexa bearer ao recurso da API %s', (url) => {
    TestBed.inject(HttpClient).get(url).subscribe();
    const pedido = http.expectOne(url);
    expect(pedido.request.headers.get('Authorization')).toBe('Bearer token-apenas-memoria');
    expect(pedido.request.withCredentials).toBe(false);
    pedido.flush({});
  });

  it.each([
    `${URL_API}.externo.invalid/api/v1/clientes`, 'http://127.0.0.1:52850/api/v1/clientes',
    'https://127.0.0.1:5285/api/v1/clientes', `${URL_API}/publico`, `${URL_API}/api/v10/clientes`,
    `${URL_API}/api/v1/../../publico`, '/assets/publico', '//externo.invalid/api/v1/clientes',
    'http://externo.invalid/api/v1/clientes',
  ])('não anexa bearer fora da origem e caminho autorizados: %s', (url) => {
    TestBed.inject(HttpClient).get(url).subscribe();
    const pedido = http.expectOne(url);
    expect(pedido.request.headers.has('Authorization')).toBe(false);
    pedido.flush({});
  });

  it('protege todas as rotas cadastrais lazy e exige login sem token', () => {
    for (const caminho of ['grupos-planos', 'veiculos', 'clientes', 'precos-servicos']) {
      const rota = routes.find((item) => item.path === caminho)!;
      expect(rota.loadComponent).toBeTypeOf('function');
      expect(rota.canActivate).toContain(authGuard);
    }
    TestBed.inject(MemoriaSessao).limparCredenciais();
    const destino = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never)) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(destino)).toBe('/entrar');
    TestBed.inject(MemoriaSessao).accessToken.set('novo-token');
    expect(TestBed.runInInjectionContext(() => authGuard({} as never, {} as never))).toBe(true);
  });
});
