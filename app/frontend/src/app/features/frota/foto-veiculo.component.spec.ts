import { HttpErrorResponse } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MemoriaSessao } from '../../core/memoria-sessao';
import { FotoVeiculoComponent } from './foto-veiculo.component';
import { Recurso } from './grupos-planos.service';
import { FotoVeiculo, Veiculo, VeiculosService } from './veiculos.service';

const foto: FotoVeiculo = { id: 'foto-a', caminho: '/api/v1/veiculos/v/foto', mime: 'image/png', tamanhoBytes: 3, largura: 1, altura: 1 };
const veiculo: Veiculo = { id: 'v', placa: 'FIC1A01', marca: 'Marca', modelo: 'Horizonte', cor: 'Prata',
  combustivel: 'Gasolina', capacidadeLitros: '50', ano: 2026, grupoId: 'g', foto: null, possuiAluguelAberto: false };

@Component({
  imports: [FotoVeiculoComponent],
  template: `<app-foto-veiculo [recurso]="atual()" [alterado]="dirty()" (atualizado)="atual.set($event)" (ocupado)="busy.set($event)" />`,
})
class Hospede {
  readonly atual = signal<Recurso<Veiculo> | null>({ dados: veiculo, etag: '"v1"' });
  readonly dirty = signal(false);
  readonly busy = signal(false);
}

describe('FotoVeiculoComponent', () => {
  let fixture: ComponentFixture<Hospede>;
  let tela: HTMLElement;
  const api = { lerFoto: vi.fn(), enviarFoto: vi.fn(), removerFoto: vi.fn(), consultar: vi.fn() };
  const criar = vi.fn();
  const revogar = vi.fn();

  beforeEach(async () => {
    vi.resetAllMocks();
    let numero = 0;
    criar.mockImplementation(() => `blob:sintetico-${++numero}`);
    vi.stubGlobal('URL', class extends URL {
      static override createObjectURL = criar;
      static override revokeObjectURL = revogar;
    });
    api.lerFoto.mockResolvedValue(new Blob(['foto'], { type: 'image/png' }));
    api.enviarFoto.mockResolvedValue({ dados: foto, etag: '"v2"' });
    api.removerFoto.mockResolvedValue('"v3"');
    await TestBed.configureTestingModule({ imports: [Hospede], providers: [{ provide: VeiculosService, useValue: api }] }).compileComponents();
    TestBed.inject(MemoriaSessao).accessToken.set('sessao-sintetica');
    fixture = TestBed.createComponent(Hospede);
    tela = fixture.nativeElement;
    document.body.appendChild(tela);
    await estabilizar();
  });
  afterEach(() => { fixture.destroy(); document.body.replaceChildren(); vi.unstubAllGlobals(); });
  async function estabilizar(): Promise<void> {
    await fixture.whenStable();
    await new Promise(resolve => setTimeout(resolve, 5));
    await fixture.whenStable();
  }
  function botao(texto: string): HTMLButtonElement {
    return [...tela.querySelectorAll('button')].find(b => b.textContent?.trim() === texto)!;
  }
  async function clicar(texto: string): Promise<void> { botao(texto).click(); await estabilizar(); }
  async function selecionar(arquivo = new File(['imagem'], 'nao-logar.png', { type: 'image/png' })): Promise<void> {
    const input = tela.querySelector<HTMLInputElement>('input[type=file]')!;
    Object.defineProperty(input, 'files', { configurable: true, value: [arquivo] });
    input.dispatchEvent(new Event('change'));
    await estabilizar();
  }

  it('separa seleção, descarte e envio explícito com nova versão e leitura autenticada', async () => {
    expect(tela.textContent).toContain('Sem foto');
    await selecionar();
    expect(tela.textContent).toContain('Prévia não enviada');
    expect(api.enviarFoto).not.toHaveBeenCalled();
    await clicar('Descartar seleção');
    expect(revogar).toHaveBeenCalledWith('blob:sintetico-1');
    await selecionar();
    await clicar('Enviar foto');
    expect(api.enviarFoto).toHaveBeenCalledTimes(1);
    expect(api.enviarFoto.mock.calls[0].slice(0, 2)).toEqual(['v', '"v1"']);
    expect(fixture.componentInstance.atual()?.etag).toBe('"v2"');
    expect(tela.textContent).toContain('Foto adicionada');
    expect(api.lerFoto).toHaveBeenCalledWith('v');
    await clicar('Remover foto');
    expect(api.removerFoto).toHaveBeenCalledWith('v', '"v2"');
    expect(tela.textContent).toContain('Foto removida');
    expect(tela.textContent).toContain('Sem foto');
  });

  it('troca somente após confirmação, bloqueia dirty/aluguel e mantém imagem anterior na falha conhecida', async () => {
    fixture.componentInstance.atual.set({ dados: { ...veiculo, foto }, etag: '"v1"' });
    // Mesma identidade: leitura manual cobre referência recebida após a criação.
    fixture.componentInstance.atual.set({ dados: { ...veiculo, id: 'outro', foto }, etag: '"v1"' });
    await estabilizar();
    const anterior = tela.querySelector('img')?.getAttribute('src');
    fixture.componentInstance.dirty.set(true);
    await estabilizar();
    expect(botao('Selecionar outra imagem').disabled).toBe(true);
    expect(tela.textContent).toContain('Salve ou descarte');
    fixture.componentInstance.dirty.set(false);
    await estabilizar();
    api.enviarFoto.mockRejectedValue(new HttpErrorResponse({ status: 503, error: { code: 'foto_indisponivel' } }));
    await selecionar();
    await clicar('Substituir foto');
    expect(tela.textContent).toContain('preservados');
    expect(fixture.componentInstance.atual()?.dados.foto?.id).toBe('foto-a');
    await clicar('Descartar seleção');
    expect(tela.querySelector('img')?.getAttribute('src')).toBe(anterior);
    fixture.componentInstance.atual.update(atual => ({ ...atual!, dados: { ...atual!.dados, possuiAluguelAberto: true } }));
    await estabilizar();
    expect(botao('Remover foto').disabled).toBe(true);
    expect(tela.textContent).toContain('Aluguel em aberto');
  });

  it.each([400, 413, 415])('associa arquivo inválido %s e foca resumo', async status => {
    api.enviarFoto.mockRejectedValue(new HttpErrorResponse({ status }));
    await selecionar();
    await clicar('Enviar foto');
    expect(document.activeElement?.id).toBe('erro-foto-veiculo');
    expect(tela.querySelector('#selecionar-foto')?.getAttribute('aria-describedby')).toContain('erro-arquivo-foto');
    expect(tela.querySelector('#erro-arquivo-foto')?.textContent).toContain('Imagem inválida');
  });

  it('rejeita seleção vazia, formato e excesso antes do envio', async () => {
    for (const arquivo of [new File([], 'v.png', { type: 'image/png' }), new File(['x'], 'x.gif', { type: 'image/gif' }),
      new File([new Uint8Array(5242881)], 'grande.png', { type: 'image/png' })]) {
      await selecionar(arquivo);
      expect(tela.textContent).toContain('Imagem inválida');
    }
    expect(api.enviarFoto).not.toHaveBeenCalled();
    expect(criar).not.toHaveBeenCalled();
  });

  it('falha de decode da prévia limpa seleção e foca erro sem enviar arquivo', async () => {
    await selecionar();
    tela.querySelector('img')!.dispatchEvent(new Event('error'));
    await estabilizar();
    expect(tela.textContent).toContain('Não foi possível apresentar a prévia');
    expect(tela.querySelector('#enviar-foto')).toBeNull();
    expect(document.activeElement?.id).toBe('erro-foto-veiculo');
    expect(api.enviarFoto).not.toHaveBeenCalled();
    expect(revogar).toHaveBeenCalledWith('blob:sintetico-1');
  });

  it.each([409, 412, 428])('conflito %s exige recarregar e nova seleção sem repetir multipart', async status => {
    api.enviarFoto.mockRejectedValue(new HttpErrorResponse({ status }));
    await selecionar();
    await clicar('Enviar foto');
    expect(botao('Selecionar imagem').disabled).toBe(true);
    api.consultar.mockResolvedValue({ dados: { ...veiculo, foto }, etag: '"v9"' });
    await clicar('Recarregar cadastro');
    expect(tela.textContent).toContain('referência atual');
    expect(api.enviarFoto).toHaveBeenCalledTimes(1);
    expect(tela.textContent).not.toContain('Foto adicionada');
    expect(tela.querySelector('#enviar-foto')).toBeNull();
  });

  it.each([0, 500, 503])('resposta incerta %s bloqueia até detalhe e leitura atuais; falha de leitura não libera mutação', async status => {
    api.enviarFoto.mockRejectedValue(new HttpErrorResponse({ status, error: { code: 'dependencia_indisponivel' } }));
    await selecionar();
    await clicar('Enviar foto');
    expect(tela.textContent).toContain('Resultado não confirmado');
    api.consultar.mockResolvedValue({ dados: { ...veiculo, foto }, etag: '"v9"' });
    api.lerFoto.mockRejectedValue(new HttpErrorResponse({ status: 503 }));
    await clicar('Recarregar cadastro');
    expect(botao('Selecionar outra imagem').disabled).toBe(true);
    api.lerFoto.mockResolvedValue(new Blob(['nova'], { type: 'image/png' }));
    await clicar('Recarregar cadastro');
    expect(botao('Selecionar outra imagem').disabled).toBe(false);
    expect(api.enviarFoto).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance.atual()?.etag).toBe('"v9"');
    expect(tela.textContent).not.toContain('Foto adicionada');
  });

  it('remoção incerta não repete DELETE e reconcilia ausência sem anunciar autoria', async () => {
    fixture.componentInstance.atual.set({ dados: { ...veiculo, id: 'com-foto', foto }, etag: '"v1"' });
    await estabilizar();
    api.removerFoto.mockRejectedValue(new HttpErrorResponse({ status: 0 }));
    await clicar('Remover foto');
    expect(tela.textContent).toContain('Resultado não confirmado');
    api.consultar.mockResolvedValue({ dados: { ...veiculo, id: 'com-foto', foto: null }, etag: '"v9"' });
    await clicar('Recarregar cadastro');
    expect(api.removerFoto).toHaveBeenCalledTimes(1);
    expect(tela.textContent).toContain('Sem foto');
    expect(tela.textContent).not.toContain('Foto removida');
    expect(fixture.componentInstance.atual()?.etag).toBe('"v9"');
  });

  it('reconsulta sem foto limpa erro de leitura anterior e mostra ausência real', async () => {
    api.lerFoto.mockRejectedValue(new HttpErrorResponse({ status: 503 }));
    fixture.componentInstance.atual.set({ dados: { ...veiculo, id: 'com-foto', foto }, etag: '"v1"' });
    await estabilizar();
    expect(tela.textContent).toContain('Não foi possível carregar a foto');
    api.removerFoto.mockRejectedValue(new HttpErrorResponse({ status: 0 }));
    await clicar('Remover foto');
    api.consultar.mockResolvedValue({ dados: { ...veiculo, id: 'com-foto', foto: null }, etag: '"v9"' });
    await clicar('Recarregar cadastro');
    expect(tela.textContent).toContain('Sem foto');
    expect(tela.textContent).not.toContain('Foto temporariamente indisponível');
    expect(botao('Tentar novamente')).toBeUndefined();
  });

  it('destruição e fechamento revogam URLs sem reaplicar imagem ou foco tardio', async () => {
    fixture.componentInstance.atual.set({ dados: { ...veiculo, id: 'com-foto', foto }, etag: '"v1"' });
    await estabilizar();
    await selecionar();
    fixture.componentInstance.atual.set(null);
    await estabilizar();
    expect(revogar).toHaveBeenCalledWith('blob:sintetico-1');
    expect(revogar).toHaveBeenCalledWith('blob:sintetico-2');
    fixture.componentInstance.atual.set({ dados: { ...veiculo, foto }, etag: '"v1"' });
    await estabilizar();
    fixture.destroy();
    expect(revogar).toHaveBeenCalledWith('blob:sintetico-3');
  });

  it('envio único mantém gate enquanto há requisição e não reaplica resposta após troca', async () => {
    let concluir!: (valor: Recurso<FotoVeiculo>) => void;
    api.enviarFoto.mockReturnValue(new Promise(resolve => { concluir = resolve; }));
    await selecionar();
    botao('Enviar foto').click();
    await estabilizar();
    expect(fixture.componentInstance.busy()).toBe(true);
    botao('Enviar foto').click();
    expect(api.enviarFoto).toHaveBeenCalledTimes(1);
    fixture.componentInstance.atual.set({ dados: { ...veiculo, id: 'novo' }, etag: '"v4"' });
    await estabilizar();
    concluir({ dados: foto, etag: '"v2"' });
    await estabilizar();
    expect(fixture.componentInstance.atual()?.dados.id).toBe('novo');
    expect(tela.textContent).not.toContain('Foto adicionada');
  });

  it('leitura indisponível oferece retry e descarta URLs na troca, saída, logout e destruição', async () => {
    api.lerFoto.mockRejectedValueOnce(new HttpErrorResponse({ status: 503 }));
    fixture.componentInstance.atual.set({ dados: { ...veiculo, id: 'com-foto', foto }, etag: '"v1"' });
    await estabilizar();
    expect(tela.textContent).toContain('Não foi possível carregar a foto');
    await clicar('Tentar novamente');
    expect(tela.querySelector('img')?.getAttribute('src')).toBe('blob:sintetico-1');
    await selecionar();
    TestBed.inject(MemoriaSessao).limparCredenciais();
    await estabilizar();
    expect(revogar).toHaveBeenCalledWith('blob:sintetico-1');
    expect(revogar).toHaveBeenCalledWith('blob:sintetico-2');
    expect(tela.querySelector('img')).toBeNull();
    expect(tela.textContent).toContain('Sessão encerrada');
  });

  it('nega sessão em resposta de leitura e ignora leitura atrasada após fechar', async () => {
    let concluir!: (blob: Blob) => void;
    api.lerFoto.mockReturnValue(new Promise(resolve => { concluir = resolve; }));
    fixture.componentInstance.atual.set({ dados: { ...veiculo, id: 'com-foto', foto }, etag: '"v1"' });
    await estabilizar();
    fixture.componentInstance.atual.set(null);
    await estabilizar();
    concluir(new Blob(['privada'], { type: 'image/png' }));
    await estabilizar();
    expect(criar).not.toHaveBeenCalled();
    api.lerFoto.mockRejectedValue(new HttpErrorResponse({ status: 401 }));
    fixture.componentInstance.atual.set({ dados: { ...veiculo, foto }, etag: '"v1"' });
    await estabilizar();
    expect(tela.textContent).toContain('Sessão encerrada');
    expect(tela.querySelector('img')).toBeNull();
  });
});
