import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ShellCadastralComponent } from './shell-cadastral.component';

describe('ShellCadastralComponent', () => {
  it('oferece os quatro destinos e um salto para o conteúdo sem persistir sessão', async () => {
    await TestBed.configureTestingModule({
      imports: [ShellCadastralComponent], providers: [provideZonelessChangeDetection(), provideRouter([])],
    }).compileComponents();
    const fixture = TestBed.createComponent(ShellCadastralComponent);
    fixture.componentRef.setInput('conteudoId', 'conteudo-teste');
    await fixture.whenStable();
    const tela = fixture.nativeElement as HTMLElement;
    expect(tela.querySelector('a')?.getAttribute('href')).toBe('#conteudo-teste');
    const destinos = [...tela.querySelectorAll('nav a')].map((item) => item.getAttribute('href'));
    expect(destinos).toEqual(['/grupos-planos', '/veiculos', '/clientes', '/precos-servicos', '/alugueis', '/painel']);
    expect(tela.querySelector('nav')?.getAttribute('aria-label')).toBe('Navegação cadastral');
    expect(tela.querySelector('select')).toBeNull();
    fixture.destroy();
  });
});
