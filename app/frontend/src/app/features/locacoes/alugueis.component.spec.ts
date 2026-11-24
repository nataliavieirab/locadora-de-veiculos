import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { AlugueisComponent } from './alugueis.component';
import { AlugueisService, ContratoAluguel, ItemListaAluguel } from './alugueis.service';
import { GruposPlanosService } from '../frota/grupos-planos.service';
import { VeiculosService } from '../frota/veiculos.service';
import { PessoasService } from '../pessoas/pessoas.service';
import { PrecosService } from '../precos/precos.service';
import { HttpErrorResponse } from '@angular/common/http';

describe('AlugueisComponent', () => {
  let fixture: ComponentFixture<AlugueisComponent>;
  let tela: HTMLElement;
  const originais = ['showModal', 'close'].map((nome) => [nome, Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, nome)] as const);
  const item: ItemListaAluguel = {
    id: '00000000-0000-0000-0000-000000000101',
    estado: 'EmAberto',
    revisao: 1,
    condutorNome: 'Condutor Exemplo',
    condutorCpfResumo: '***.***.***-32',
    veiculoPlaca: 'ABC-1D23',
    planoModalidade: 'Diario',
    saidaUtc: '2026-11-23T13:00:00Z',
    retornoPrevistoUtc: '2026-11-25T13:00:00Z',
    totalConhecido: '254.00',
  };
  const contrato: ContratoAluguel = {
    id: item.id, estado: 'EmAberto', revisao: 1, clienteId: 'c', condutorId: 'd', veiculoId: 'v', planoId: 'p',
    saidaUtc: item.saidaUtc, retornoPrevistoUtc: item.retornoPrevistoUtc, hodometroInicial: 1000, caucao: '1000.00',
    cliente: {
      tipo: 'PF', nome: 'Cliente Original', cpf: '123', cnpj: null, rg: null, cnh: null, telefone: '11999999999',
      email: null, endereco: { estado: 'SP', cidade: 'São Paulo', bairro: 'Centro', rua: 'Rua A', numero: '1' },
    },
    condutor: { nome: 'Condutor Exemplo', cpf: '123', cnh: 'cnh', validadeCnh: '2028-01-01', email: 'a@b.invalid', telefone: '11' },
    veiculo: {
      placa: 'ABC-1D23', marca: 'Marca', cor: 'Azul', modelo: 'Modelo', combustivel: 'Gasolina',
      capacidadeLitros: '50.000', ano: 2026, grupoId: 'g', grupoNome: 'Compactos',
    },
    plano: { modalidade: 'Diario', precoDiario: '100.00', precoKm: '2.00', franquiaDiariaKm: null, precoKmExcedente: null },
    itens: [],
    estimativa: {
      versaoRegra: 'v1', calculadoEmUtc: item.saidaUtc, saidaUtc: item.saidaUtc, retornoPrevistoUtc: item.retornoPrevistoUtc,
      diarias: 2, quilometros: null, franquiaTotalKm: null, quilometragemPendente: true, combustivelPendente: true,
      atrasoPendente: true, termoDiariasBruto: '200', termoKmBruto: '0', componentes: [], totalConhecido: '254.00', caucao: '1000.00',
    },
  };
  const api = { listar: vi.fn(), consultar: vi.fn(), todasReferenciasVeiculos: vi.fn(), ocultar: vi.fn() };
  const frota = { listar: vi.fn(), listarDisponiveis: vi.fn().mockResolvedValue([]) };
  const pessoas = { listarTodosClientes: vi.fn().mockResolvedValue([]), listarCondutoresDoCliente: vi.fn().mockResolvedValue([]) };
  const grupos = {
    listarGrupos: vi.fn(async (page = 1) => ({ items: [], page, pageSize: 20, total: 0 })),
  };
  const precos = {
    listarTodasTaxas: vi.fn().mockResolvedValue([]),
    seguro: vi.fn().mockImplementation((tipo: string) => Promise.resolve({
      dados: { configurado: false, tipo, precoDiario: null, id: null },
      etag: null,
    })),
  };

  beforeEach(async () => {
    vi.resetAllMocks();
    api.listar.mockResolvedValue({ items: [item], page: 1, pageSize: 20, total: 1 });
    api.consultar.mockResolvedValue({ dados: contrato, etag: '"v1"' });
    api.todasReferenciasVeiculos.mockResolvedValue([{ id: 'v', placa: 'ABC-1D23', modelo: 'Modelo' }]);
    api.ocultar.mockResolvedValue(undefined);
    frota.listar.mockResolvedValue({ items: [{ id: 'v', placa: 'ABC-1D23', modelo: 'Modelo' }], page: 1, pageSize: 20, total: 1 });
    frota.listarDisponiveis.mockResolvedValue([]);
    pessoas.listarTodosClientes.mockResolvedValue([]);
    pessoas.listarCondutoresDoCliente.mockResolvedValue([]);
    grupos.listarGrupos.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 });
    precos.listarTodasTaxas.mockResolvedValue([]);
    precos.seguro.mockImplementation((tipo: string) => Promise.resolve({
      dados: { configurado: false, tipo, precoDiario: null, id: null }, etag: null,
    }));
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = true; },
    });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = false; this.dispatchEvent(new Event('close')); },
    });
    await TestBed.configureTestingModule({
      imports: [AlugueisComponent],
      providers: [
        provideZonelessChangeDetection(), provideRouter([]),
        { provide: AlugueisService, useValue: api }, { provide: VeiculosService, useValue: frota },
        { provide: PessoasService, useValue: pessoas }, { provide: GruposPlanosService, useValue: grupos },
        { provide: PrecosService, useValue: precos },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AlugueisComponent);
    tela = fixture.nativeElement;
    document.body.appendChild(tela);
    await estabilizar();
  });

  afterEach(() => {
    fixture.destroy();
    document.body.replaceChildren();
    for (const [nome, original] of originais) {
      if (original) Object.defineProperty(HTMLDialogElement.prototype, nome, original);
      else Reflect.deleteProperty(HTMLDialogElement.prototype, nome);
    }
  });

  async function estabilizar(): Promise<void> {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    await fixture.whenStable();
  }

  it('abre a rota de devolução pela lista e pelo drawer sem criar menu novo', async () => {
    const navegar = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const devolver = Array.from(tela.querySelectorAll<HTMLButtonElement>('table button'))
      .find(b => b.textContent?.trim() === 'Registrar devolução')!;
    devolver.click(); await estabilizar();
    expect(navegar).toHaveBeenCalledWith(`/alugueis/${item.id}/devolucao`);
    const consultar = tela.querySelector<HTMLButtonElement>('table button')!;
    consultar.click(); await estabilizar(); fixture.detectChanges();
    const acaoDrawer = Array.from(tela.querySelectorAll<HTMLButtonElement>('dialog footer button'))
      .find(b => b.textContent?.trim() === 'Registrar devolução')!;
    acaoDrawer.click(); await estabilizar();
    expect(navegar).toHaveBeenCalledTimes(2);
    expect(tela.querySelector('dialog')?.open).toBe(false);
    expect(tela.querySelector('nav')?.textContent).not.toContain('Devoluções');
  });

  it('navega para a rota própria de contratação e carrega o formulário', async () => {
    const navegar = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    await estabilizar();
    tela.querySelector<HTMLButtonElement>('#nova-locacao')!.click();
    expect(navegar).toHaveBeenCalledWith('/alugueis/nova');
    TestBed.inject(ActivatedRoute).snapshot.data = { nova: true };
    await fixture.componentInstance.ngOnInit();
    await estabilizar();
    await estabilizar();
    expect(tela.textContent).not.toContain('Carregando dados para contratação');
    expect(tela.textContent).toContain('Cliente e condutor');
    expect(tela.textContent).toContain('No momento da confirmação');
    tela.querySelector<HTMLButtonElement>('app-contratacao-locacao > button')!.click();
    await estabilizar();
    expect(navegar).toHaveBeenCalledWith('/alugueis');
  });

  it('distingue estimativa de final e identifica ocultos sem ações de edição/devolução', async () => {
    api.listar.mockResolvedValue({ items: [
      item, { ...item, id: 'final', estado: 'Concluido', totalConhecido: '254.00', totalFinal: '334.00', oculto: true },
    ], page: 1, pageSize: 20, total: 2 });
    await fixture.componentInstance['carregar'](); fixture.detectChanges();
    const linhas = tela.querySelectorAll('tbody tr');
    expect(linhas[0].textContent).toContain('Estimativa · parcelas conhecidas');
    expect(linhas[1].textContent).toContain('334,00');
    expect(linhas[1].textContent).not.toContain('254,00');
    expect(linhas[1].textContent).toContain('Total final');
    expect(linhas[1].textContent).toContain('Concluído');
    expect(linhas[1].textContent).toContain('Oculto da operação');
    expect(linhas[1].textContent).not.toContain('Registrar devolução');
    expect(linhas[1].textContent).not.toContain('Editar');
  });

  it('aplica/limpa ocultos e mantém filtros já aplicados durante paginação', async () => {
    fixture.componentInstance['incluirOcultos'].set(true);
    fixture.componentInstance['filtroEstado'].set('Concluido');
    await fixture.componentInstance['aplicarFiltros'](new Event('submit'));
    expect(api.listar).toHaveBeenLastCalledWith(1, 20, { estado: 'Concluido', incluirOcultos: true });
    expect(api.todasReferenciasVeiculos).toHaveBeenLastCalledWith(true);
    fixture.componentInstance['filtroEstado'].set('EmAberto');
    fixture.componentInstance['incluirOcultos'].set(false);
    await fixture.componentInstance['carregar'](2);
    expect(api.listar).toHaveBeenLastCalledWith(2, 20, { estado: 'Concluido', incluirOcultos: true });
    await fixture.componentInstance['limparFiltros']();
    expect(api.listar).toHaveBeenLastCalledWith(1, 20, {});
    expect(api.todasReferenciasVeiculos).toHaveBeenLastCalledWith(false);
    expect(fixture.componentInstance['incluirOcultos']()).toBe(false);
  });

  it('usa referências históricas completas e não a frota ativa para o seletor', async () => {
    api.todasReferenciasVeiculos.mockResolvedValue(Array.from({ length: 21 }, (_, i) =>
      ({ id: `historico-${i}`, placa: `FIC${i}`, modelo: 'Snapshot preservado' })));
    await fixture.componentInstance['carregar'](); fixture.detectChanges();
    expect(tela.querySelectorAll('#filtro-veiculo option')).toHaveLength(22);
    expect(tela.querySelector('#filtro-veiculo')?.textContent).toContain('FIC20');
    expect(frota.listar).not.toHaveBeenCalled();
  });

  it('confirma ocultação com versão lida e restaura foco ao título quando a linha sai', async () => {
    const final = { ...item, estado: 'Concluido', totalFinal: '334.00', oculto: false };
    api.listar.mockResolvedValue({ items: [final], page: 1, pageSize: 20, total: 1 });
    api.consultar.mockResolvedValue({ dados: { ...contrato, estado: 'Concluido' }, etag: '"final"' });
    await fixture.componentInstance['carregar'](); fixture.detectChanges();
    const botao = Array.from(tela.querySelectorAll<HTMLButtonElement>('table button')).find(b => b.textContent?.trim() === 'Ocultar da operação')!;
    botao.click(); await estabilizar(); fixture.detectChanges();
    expect(document.activeElement?.id).toBe('voltar-sem-ocultar');
    api.listar.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 });
    await fixture.componentInstance['confirmarOcultacao'](); fixture.detectChanges();
    expect(api.ocultar).toHaveBeenCalledWith(item.id, '"final"');
    expect(tela.textContent).toContain('Locação ocultada da operação');
    expect(tela.textContent).toContain('Nenhum aluguel cadastrado');
    expect(document.activeElement?.id).toBe('titulo-alugueis');
  });

  it('consulta final oculto explicitamente com snapshots mascarados e sem ações mutáveis', async () => {
    fixture.componentInstance['incluirOcultos'].set(true);
    await fixture.componentInstance['aplicarFiltros'](new Event('submit'));
    api.consultar.mockResolvedValue({ dados: { ...contrato, estado: 'Concluido', oculto: true,
      devolucao: { id: 'final-preservado', calculo: {
        retornoRealUtc: '2026-11-25T13:00:00Z', hodometroFinal: 1150, litrosRestantes: '40.00', capacidadeLitros: '50.00',
        diarias: 2, quilometros: 150, franquiaTotalKm: null, excedenteKm: null, emAtraso: false,
        termoDiariasBruto: '200.00', termoKmBruto: '0.00', componentes: [], litrosRepostos: '10.00',
        combustivel: { tipo: 'Gasolina', id: 'versao-f', precoUnitario: '6.00', inicioVigenciaUtc: '2026-11-23T13:00:00Z' },
        totalFinal: '334.00', caucao: '1000.00', versaoRegra: 'devolucao-v1',
      } } }, etag: '"v4"' });
    await fixture.componentInstance['abrirConsulta']({ ...item, estado: 'Concluido', oculto: true },
      { currentTarget: tela.querySelector('table button') } as unknown as Event);
    fixture.detectChanges();
    const drawer = tela.querySelector('dialog')!;
    expect(api.consultar).toHaveBeenLastCalledWith(item.id, true);
    expect(drawer.textContent).toContain('Final imutável');
    expect(drawer.textContent).toContain('334,00');
    expect(drawer.textContent).toContain('versao-f');
    expect(drawer.textContent).not.toContain('Demonstrativo estimado');
    expect(drawer.textContent).not.toContain('Registrar devolução');
    expect(drawer.textContent).not.toContain('Editar contrato');
    expect(drawer.textContent).not.toContain('Ocultar da operação');
    expect(drawer.textContent).not.toContain('Restaurar');
  });

  it('desistir/Escape não oculta e envio impede fechar o diálogo', async () => {
    const final = { ...item, estado: 'Concluido', totalFinal: '334.00' };
    const evento = { currentTarget: tela.querySelector('table button') } as unknown as Event;
    await fixture.componentInstance['pedirOcultacao'](final, evento); fixture.detectChanges();
    fixture.componentInstance['cancelarOcultacao']();
    expect(api.ocultar).not.toHaveBeenCalled();
    await fixture.componentInstance['pedirOcultacao'](final, evento); fixture.detectChanges();
    let liberar!: () => void;
    api.ocultar.mockReturnValueOnce(new Promise<void>(r => liberar = r));
    const envio = fixture.componentInstance['confirmarOcultacao']();
    const escape = new Event('cancel', { cancelable: true });
    fixture.componentInstance['cancelarOcultacao'](escape);
    expect(escape.defaultPrevented).toBe(true);
    expect(fixture.componentInstance.podeSair()).toBe(false);
    expect(tela.querySelectorAll('dialog')[1].open).toBe(true);
    liberar(); await envio;
  });

  it('erro/412 na ocultação exige atualizar consulta sem afirmar sucesso nem repetir DELETE', async () => {
    const final = { ...item, estado: 'Concluido', totalFinal: '334.00' };
    for (const status of [0, 412]) {
      await fixture.componentInstance['pedirOcultacao'](final, { currentTarget: tela.querySelector('table button') } as unknown as Event);
      api.ocultar.mockRejectedValueOnce(new HttpErrorResponse({ status }));
      await fixture.componentInstance['confirmarOcultacao'](); fixture.detectChanges();
      expect(fixture.componentInstance['verificarOcultacao']()).toBe(true);
      expect(tela.textContent).not.toContain('Locação ocultada da operação');
      expect(document.activeElement?.id).toBe('erro-ocultacao');
      const chamadas = api.ocultar.mock.calls.length;
      await fixture.componentInstance['confirmarOcultacao']();
      expect(api.ocultar).toHaveBeenCalledTimes(chamadas);
      await fixture.componentInstance['atualizarAposOcultacao']();
    }
  });

  it('lista aberto como estimativa e oferece somente as ações permitidas', async () => {
    await estabilizar();
    expect(tela.textContent).toContain('Condutor Exemplo');
    expect(tela.textContent).toContain('Estimativa · parcelas conhecidas');
    expect(tela.querySelector('tbody tr')?.textContent).toContain('Em aberto');
    expect(tela.querySelector('tbody tr')?.textContent).not.toContain('Concluído');
    expect(tela.querySelector('tbody tr')?.textContent).toContain('Registrar devolução');
    expect(tela.querySelector('button')?.textContent).not.toMatch(/devolver|registrar devolução/i);
    expect(tela.querySelector('button[aria-label^="Consultar contrato"]')).toBeTruthy();
    expect(tela.querySelector('button[aria-label^="Editar"]')).toBeTruthy();
  });

  it('rejeita período inválido e aplica filtros', async () => {
    await estabilizar();
    const de = tela.querySelector<HTMLInputElement>('#filtro-saida-de')!;
    const ate = tela.querySelector<HTMLInputElement>('#filtro-saida-ate')!;
    de.value = '2026-11-25';
    de.dispatchEvent(new Event('change', { bubbles: true }));
    ate.value = '2026-11-23';
    ate.dispatchEvent(new Event('change', { bubbles: true }));
    tela.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await estabilizar();
    expect(tela.textContent).toContain('O fim do período deve ser igual ou posterior ao início');
    expect(api.listar).toHaveBeenCalledTimes(1);
    de.value = '2026-11-23';
    de.dispatchEvent(new Event('change', { bubbles: true }));
    ate.value = '2026-11-25';
    ate.dispatchEvent(new Event('change', { bubbles: true }));
    tela.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await estabilizar();
    expect(api.listar).toHaveBeenCalledTimes(2);
  });

  it('abre consulta lateral com demonstrativo e retorna foco ao fechar', async () => {
    await estabilizar();
    const botao = tela.querySelector<HTMLButtonElement>('button[aria-label^="Consultar contrato"]')!;
    botao.focus();
    botao.click();
    await estabilizar();
    expect(api.consultar).toHaveBeenCalledWith(item.id, false);
    expect(tela.textContent).toContain('Cliente Original');
    expect(tela.textContent).toContain('Não é o valor final da locação');
    expect(tela.textContent).toContain('caução fixa');
    expect(tela.textContent).toContain('01/01/2028');
    expect(tela.textContent).toContain('50,000 L');
    tela.querySelector<HTMLDialogElement>('dialog')!.dispatchEvent(new Event('close'));
    await estabilizar();
    expect(document.activeElement).toBe(botao);
  });

  it('recupera lista após erro com tentar novamente', async () => {
    await estabilizar();
    api.listar.mockRejectedValueOnce(new Error('falha'));
    tela.querySelector<HTMLButtonElement>('[type="submit"]')!.click();
    await estabilizar();
    expect(tela.textContent).toContain('Não foi possível concluir a operação');
    api.listar.mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 0 });
    [...tela.querySelectorAll('button')].find((b) => b.textContent?.includes('Tentar novamente'))!.click();
    await estabilizar();
    expect(tela.textContent).toContain('Nenhum aluguel cadastrado');
  });
});
