import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MENSAGEM_CREDENCIAL, URL_API } from '../../core/configuracao';
import { apiInterceptor } from '../../core/api.interceptor';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    document.body.replaceChildren();
    http.verify();
  });

  it('leva o foco ao resumo e não revela se o usuário existe', async () => {
    const fixture = TestBed.createComponent(LoginComponent);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();

    const usuario = fixture.nativeElement.querySelector('#usuario') as HTMLInputElement;
    const senha = fixture.nativeElement.querySelector('#senha') as HTMLInputElement;
    usuario.value = 'ana';
    usuario.dispatchEvent(new Event('input', { bubbles: true }));
    senha.value = 'errada12';
    senha.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();

    const formulario = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    formulario.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const pedido = http.expectOne(`${URL_API}/api/v1/session/login`);
    pedido.flush({ title: 'usuario nao existe' }, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 20));
    fixture.detectChanges();

    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain(MENSAGEM_CREDENCIAL);
    expect(texto.toLowerCase()).not.toContain('não existe');
    expect(texto.toLowerCase()).not.toContain('nao existe');
    expect(texto.toLowerCase()).not.toContain('incorreta');
    expect(document.activeElement?.id).toBe('resumo-erro');
  });
});
