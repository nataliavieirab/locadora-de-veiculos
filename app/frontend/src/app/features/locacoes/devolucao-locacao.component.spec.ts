import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { DevolucaoLocacaoComponent } from './devolucao-locacao.component';
import { AlugueisService, ContratoAluguel } from './alugueis.service';
import { PrecosService } from '../precos/precos.service';
import { routes } from '../../app.routes';
import { protegerDevolucao } from './proteger-devolucao.guard';
import { authGuard } from '../../core/auth.guard';
import { HttpErrorResponse } from '@angular/common/http';
import { MemoriaSessao } from '../../core/memoria-sessao';
import { PreviaDevolucao } from './devolucao.contratos';

export const contratoRetorno: ContratoAluguel = {
  id: 'aluguel-1', estado: 'EmAberto', revisao: 2, clienteId: 'c', condutorId: 'd', veiculoId: 'v', planoId: 'p',
  saidaUtc: '2026-11-22T13:00:00Z', retornoPrevistoUtc: '2026-11-24T13:00:00Z', hodometroInicial: 18420,
  caucao: '1000.00', cliente: { tipo: 'PF', nome: 'Cliente sintético', cpf: '12300000032', cnpj: null,
    rg: 'RG', cnh: 'CNH123456', telefone: '0', email: null, endereco: { estado: 'SC', cidade: 'Cidade', bairro: 'B', rua: 'R', numero: '1' } },
  condutor: { nome: 'Condutor sintético', cpf: '12300000032', cnh: 'CNH123456', validadeCnh: '2026-11-23', email: 'a@b.invalid', telefone: '0' },
  veiculo: { placa: 'FIC1A01', marca: 'Marca', cor: 'Azul', modelo: 'Modelo', combustivel: 'Gasolina', capacidadeLitros: '50.000',
    ano: 2026, grupoId: 'g', grupoNome: 'Compactos' },
  plano: { modalidade: 'Livre', precoDiario: '100.00', precoKm: null, franquiaDiariaKm: null, precoKmExcedente: null },
  itens: [{ categoria: 'TaxaServico', origemId: 't1', tipoSeguro: null, nome: 'Já contratada', modalidade: 'Fixa',
    precoUnitario: '30.00', origemInclusao: 'Contratacao', revisaoPrimeiraInclusao: 1 }],
  estimativa: { versaoRegra: 'contratacao-v1', calculadoEmUtc: '2026-11-22T13:00:00Z', saidaUtc: '2026-11-22T13:00:00Z',
    retornoPrevistoUtc: '2026-11-24T13:00:00Z', diarias: 2, quilometros: null, franquiaTotalKm: null,
    quilometragemPendente: false, combustivelPendente: true, atrasoPendente: true, termoDiariasBruto: '200.00',
    termoKmBruto: '0.00', componentes: [], totalConhecido: '230.00', caucao: '1000.00' },
};

describe('DevolucaoLocacaoComponent — formulário 7.1', () => {
  let fixture: ComponentFixture<DevolucaoLocacaoComponent>;
  let tela: HTMLElement;
  const api = { consultar: vi.fn(), previaDevolucao: vi.fn(), devolver: vi.fn() };
  const precos = { listarTodasTaxas: vi.fn() };
  const previa: PreviaDevolucao = {
    aluguelId: 'aluguel-1', revisaoId: 'r', revisao: 2, versaoAluguel: '"v2"',
    versoesAceitas: { revisaoId: 'r', versaoRegra: 'devolucao-v1', combustivelId: 'f', taxasNovas: [], resumoCalculo: 'A'.repeat(64) },
    calculo: { versaoRegra: 'devolucao-v1', saidaUtc: contratoRetorno.saidaUtc, retornoPrevistoUtc: contratoRetorno.retornoPrevistoUtc,
      retornoRealUtc: '2026-11-24T13:00:00Z', hodometroInicial: 18420, hodometroFinal: 18570,
      litrosRestantes: '40.00', capacidadeLitros: '50.00', litrosRepostos: '10.00', diarias: 2, quilometros: 150,
      franquiaTotalKm: null, excedenteKm: null, emAtraso: false, termoDiariasBruto: '200.00', termoKmBruto: '0.00',
      combustivel: { id: 'f', tipo: 'Gasolina', inicioVigenciaUtc: '2026-11-21T13:00:00Z', precoUnitario: '6.00' },
      taxasNovas: [], componentes: [], totalFinal: '334.00', caucao: '1000.00' },
  };
  beforeEach(async () => {
    vi.resetAllMocks();
    api.consultar.mockResolvedValue({ dados: structuredClone(contratoRetorno), etag: '"v2"' });
    api.previaDevolucao.mockResolvedValue(structuredClone(previa));
    api.devolver.mockResolvedValue({ dados: { aluguelId: 'aluguel-1', estado: 'Concluido', devolucao: { id: 'final', calculo: previa.calculo } }, etag: '"v3"' });
    precos.listarTodasTaxas.mockResolvedValue([
      { id: 't1', nome: 'Atual diferente', preco: '99.00', modalidade: 'Fixa' },
      { id: 't2', nome: 'Apoio de viagem', preco: '10.00', modalidade: 'Diaria' },
    ]);
    await TestBed.configureTestingModule({
      imports: [DevolucaoLocacaoComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'aluguel-1' }) } } },
        { provide: AlugueisService, useValue: api }, { provide: PrecosService, useValue: precos }],
    }).compileComponents();
    fixture = TestBed.createComponent(DevolucaoLocacaoComponent);
    tela = fixture.nativeElement;
    TestBed.inject(MemoriaSessao).contaId.set('ator');
    TestBed.inject(MemoriaSessao).empresaId.set('empresa');
  });
  async function aguardarCarga() {
    await vi.waitFor(() => expect(fixture.componentInstance['carregando']()).toBe(false));
    await fixture.whenStable(); fixture.detectChanges();
  }
  async function iniciar() { fixture.detectChanges(); await aguardarCarga(); }
  function preencher(id: string, valor: string) {
    const input = tela.querySelector<HTMLInputElement>(`#${id}`)!;
    input.value = valor; input.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
  }
  async function validar() {
    const retorno = await fixture.componentInstance['validarEntradas']();
    fixture.detectChanges();
    return retorno;
  }
  it('tem rota lazy autenticada, guard de saída e nenhum menu novo', () => {
    const rota = routes.find(r => r.path === 'alugueis/:id/devolucao')!;
    expect(rota.canActivate).toContain(authGuard);
    expect(rota.canDeactivate).toContain(protegerDevolucao);
    expect(rota.loadComponent).toBeTypeOf('function');
  });
  it('mostra carregamento, contexto preservado e somente taxas não contratadas', async () => {
    let resolver!: (valor: unknown) => void;
    api.consultar.mockReturnValue(new Promise(r => resolver = r));
    fixture.detectChanges();
    expect(tela.textContent).toContain('Carregando contrato e taxas');
    resolver({ dados: contratoRetorno, etag: '"v2"' });
    await aguardarCarga();
    expect(api.consultar).toHaveBeenCalledWith('aluguel-1');
    expect(precos.listarTodasTaxas).toHaveBeenCalledOnce();
    expect(tela.textContent).toContain('Já contratada');
    expect(tela.textContent).not.toContain('Atual diferente');
    expect(tela.textContent).not.toContain('12300000032');
    expect(tela.textContent).toContain('22/11/2026 · 10:00');
    expect(tela.querySelectorAll('input[type=checkbox]')).toHaveLength(1);
    expect(tela.querySelectorAll('input:not([type=checkbox])')).toHaveLength(3);
  });
  it('valida obrigatórios, associa mensagens e foca resumo sem enviar', async () => {
    await iniciar();
    expect(await validar()).toBeNull();
    expect(tela.querySelector('#erro-retorno')).not.toBeNull();
    expect(tela.querySelector('#erro-hodometro')).not.toBeNull();
    expect(tela.querySelector('#erro-litros')).not.toBeNull();
    expect(document.activeElement?.id).toBe('erro-devolucao');
    expect(api.previaDevolucao).not.toHaveBeenCalled();
  });
  it('aceita retorno igual à saída, vírgula decimal e CNH vencida posteriormente', async () => {
    await iniciar();
    preencher('retorno-real', '2026-11-22T10:00');
    preencher('hodometro-final', '18420');
    preencher('litros-restantes', '40,125');
    expect(await validar()).toEqual({ retornoReal: '2026-11-22T10:00:00-03:00', hodometroFinal: 18420,
      litrosRestantes: '40.125', taxasAdicionaisIds: [] });
    preencher('retorno-real', '2026-11-24T10:00');
    expect(await validar()).not.toBeNull();
  });
  it('rejeita intervalos, fração de km, overflow, escala e horários inválidos/ambíguos', async () => {
    await iniciar();
    for (const [retorno, hodometro, litros] of [
      ['2026-11-22T09:59', '18570', '40'],
      ['2026-11-24T10:00', '18419', '40'], ['2026-11-24T10:00', '18570.5', '40'],
      ['2026-11-24T10:00', '2147483648', '40'], ['2026-11-24T10:00', '18570', '50.001'],
      ['2026-11-24T10:00', '18570', '-1'], ['2026-11-24T10:00', '18570', '40.0001'],
      ['2018-11-04T00:30', '18570', '40'], ['2019-02-16T23:30', '18570', '40'],
    ]) {
      preencher('retorno-real', retorno); preencher('hodometro-final', hodometro); preencher('litros-restantes', litros);
      expect(await validar()).toBeNull();
    }
  });
  it('não remove itens contratados e conserva seleção separada', async () => {
    await iniciar();
    const input = tela.querySelector<HTMLInputElement>('input[type=checkbox]')!;
    input.checked = true; input.dispatchEvent(new Event('change', { bubbles: true }));
    preencher('retorno-real', '2026-11-24T10:00'); preencher('hodometro-final', '18570'); preencher('litros-restantes', '40');
    expect((await validar())?.taxasAdicionaisIds).toEqual(['t2']);
    expect(fixture.componentInstance['contrato']()?.itens).toEqual(contratoRetorno.itens);
  });
  it('distingue ausência de taxas de indisponibilidade e permite nova leitura', async () => {
    precos.listarTodasTaxas.mockRejectedValueOnce(new Error('SQL privado'));
    await iniciar();
    expect(tela.textContent).not.toContain('SQL privado');
    expect(tela.textContent).not.toContain('Cliente sintético');
    precos.listarTodasTaxas.mockResolvedValue([]);
    (Array.from(tela.querySelectorAll('button')).find(b => b.textContent?.includes('Tentar novamente'))!).click();
    await aguardarCarga();
    expect(tela.textContent).toContain('Não há taxas adicionais disponíveis');
  });
  it('concluído não expõe formulário nem ações de nova devolução', async () => {
    api.consultar.mockResolvedValue({ dados: { ...contratoRetorno, estado: 'Concluido' }, etag: '"v3"' });
    await iniciar();
    expect(tela.querySelector('form')).toBeNull();
    expect(tela.textContent).toContain('Não admite edição, nova devolução ou reabertura');
  });

  async function preencherValido() {
    await iniciar();
    preencher('retorno-real', '2026-11-24T10:00'); preencher('hodometro-final', '18570'); preencher('litros-restantes', '40');
    await fixture.whenStable();
  }
  async function calcular() {
    await fixture.componentInstance['calcular']();
    fixture.detectChanges();
  }
  it('calcula remotamente e invalida aceite quando as entradas mudam', async () => {
    await preencherValido(); await calcular();
    expect(api.previaDevolucao).toHaveBeenCalledWith('aluguel-1', expect.objectContaining({ litrosRestantes: '40' }), '"v2"');
    expect(tela.textContent).toContain('334,00');
    expect(tela.textContent).toContain('Prévia da devolução');
    preencher('litros-restantes', '39');
    expect(fixture.componentInstance['previa']()).toBeNull();
    expect(tela.textContent).toContain('Calcule uma nova prévia');
    await fixture.componentInstance['confirmar'](new Event('submit'));
    expect(api.devolver).not.toHaveBeenCalled();
  });
  it('ignora prévia obsoleta e congela Signal Forms e fieldset durante cálculo', async () => {
    await preencherValido();
    let resolver!: (p: PreviaDevolucao) => void;
    api.previaDevolucao.mockReturnValueOnce(new Promise(r => resolver = r));
    const calcularPendente = fixture.componentInstance['calcular']();
    await vi.waitFor(() => expect(fixture.componentInstance['calculando']()).toBe(true));
    fixture.detectChanges();
    expect(fixture.componentInstance['formulario'].litrosRestantes().disabled()).toBe(true);
    expect(tela.querySelector('fieldset')?.disabled).toBe(true);
    // Simula nova intenção/atualização externa antes da resposta; não é edição por teclado do campo desabilitado.
    fixture.componentInstance['modelo'].update(m => ({ ...m, litrosRestantes: '38' }));
    fixture.detectChanges();
    resolver(previa); await calcularPendente; fixture.detectChanges();
    expect(fixture.componentInstance['previa']()).toBeNull();
  });
  it('409 exige nova prévia; 412/428 exigem recarga mantendo entradas; falta de vigência orienta configuração', async () => {
    await preencherValido();
    for (const [status, code, trecho] of [
      [409, 'previa_desatualizada', 'Calcule uma nova prévia'],
      [409, 'combustivel_sem_vigencia', 'não preenche retroativamente'],
      [412, 'versao_divergente', 'Recarregue o contrato'],
    ] as const) {
      api.previaDevolucao.mockRejectedValueOnce(new HttpErrorResponse({ status, error: { code } }));
      await calcular();
      expect(tela.textContent).toContain(trecho);
      expect(fixture.componentInstance['modelo']().litrosRestantes).toBe('40');
      expect(fixture.componentInstance['previa']()).toBeNull();
    }
    expect(fixture.componentInstance['exigirRecarga']()).toBe(true);
    await fixture.componentInstance['carregar'](); fixture.detectChanges();
    api.previaDevolucao.mockRejectedValueOnce(new HttpErrorResponse({ status: 428 }));
    await calcular();
    expect(fixture.componentInstance['exigirRecarga']()).toBe(true);
  });
  it('resposta incerta congela intenção/chave/aceite e bloqueia navegação/recarga sem storage', async () => {
    await preencherValido(); await calcular();
    api.devolver.mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }));
    await fixture.componentInstance['confirmar'](new Event('submit')); fixture.detectChanges();
    expect(fixture.componentInstance.navegacaoBloqueada()).toBe(true);
    expect(fixture.componentInstance['formulario'].hodometroFinal().disabled()).toBe(true);
    expect(tela.querySelector('fieldset')?.disabled).toBe(true);
    expect(tela.textContent).toContain('A devolução pode ter sido registrada');
    const evento = new Event('beforeunload', { cancelable: true });
    fixture.componentInstance.protegerRecarga(evento as BeforeUnloadEvent);
    expect(evento.defaultPrevented).toBe(true);
    const original = structuredClone(api.devolver.mock.calls[0]);
    fixture.componentInstance['alternarTaxa']('t2', true);
    expect(fixture.componentInstance['taxasMarcadas']()).toEqual([]);
    await fixture.componentInstance['verificar'](); fixture.detectChanges();
    expect(api.devolver.mock.calls[1]).toEqual(original);
    expect(fixture.componentInstance.navegacaoBloqueada()).toBe(false);
    expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0);
  });
  it('401/403 não comprovam rollback e outra conta não pode verificar a intenção original', async () => {
    await preencherValido(); await calcular();
    api.devolver.mockRejectedValueOnce(new HttpErrorResponse({ status: 401 }));
    await fixture.componentInstance['confirmar'](new Event('submit')); fixture.detectChanges();
    expect(fixture.componentInstance['reautenticar']()).toBe(true);
    TestBed.inject(MemoriaSessao).contaId.set('outro');
    await fixture.componentInstance['verificar']();
    expect(api.devolver).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance['incerto']()).toBe(true);
    TestBed.inject(MemoriaSessao).contaId.set('ator');
    api.devolver.mockRejectedValueOnce(new HttpErrorResponse({ status: 403 }));
    await fixture.componentInstance['verificar']();
    expect(fixture.componentInstance['incerto']()).toBe(true);
    expect(fixture.componentInstance.navegacaoBloqueada()).toBe(true);
  });

  it('apresenta o final retornado, não a prévia, e ignora confirmação duplicada', async () => {
    await preencherValido(); await calcular();
    api.devolver.mockResolvedValue({ dados: { aluguelId: 'aluguel-1', estado: 'Concluido',
      devolucao: { id: 'final-original', calculo: { ...previa.calculo, totalFinal: '345.67' } } }, etag: '"v3"' });
    await Promise.all([
      fixture.componentInstance['confirmar'](new Event('submit')),
      fixture.componentInstance['confirmar'](new Event('submit')),
    ]);
    fixture.detectChanges();
    expect(api.devolver).toHaveBeenCalledTimes(1);
    expect(api.consultar).toHaveBeenCalledTimes(1);
    expect(tela.textContent).toContain('345,67');
    expect(tela.textContent).not.toContain('334,00');
    expect(tela.textContent).toContain('Final imutável');
    expect(tela.querySelector('fieldset')).toBeNull();
    expect(tela.querySelector('#consultar-final')).not.toBeNull();
  });
});
