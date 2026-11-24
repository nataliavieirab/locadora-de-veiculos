import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { form, FormField, FormRoot, required, validate } from '@angular/forms/signals';
import { mensagemCampo } from '../acesso/acesso-formulario';
import { MoedaBrlPipe } from '../../shared/moeda-brl.pipe';
import { ResumoErroComponent } from '../../shared/resumo-erro.component';
import { focarResumo } from '../../shared/foco';
import { datetimeLocalSaoPaulo, formatarInstanteSp, sugestaoRetornoDatetimeLocal, transporteRetornoPrevisto } from '../../shared/horario-sao-paulo';
import { problemaCadastro, ProblemaCadastro } from '../../shared/erro-cadastro';
import { Grupo, GruposPlanosService, ModalidadePlano, Plano } from '../frota/grupos-planos.service';
import { Veiculo, VeiculosService } from '../frota/veiculos.service';
import { Condutor, PessoasService, ResumoCliente } from '../pessoas/pessoas.service';
import { PrecoSeguro, PrecosService, TaxaServico } from '../precos/precos.service';
import { AlugueisService, ContratoAluguel, EntradaContratacao, EstimativaContrato } from './alugueis.service';
import { Recurso } from '../frota/grupos-planos.service';
import { SessaoService } from '../../core/sessao.service';
import { MoedaBrlPipe as FormatadorBrl } from '../../shared/moeda-brl.pipe';

interface ModeloContratacao {
  clienteId: string;
  condutorId: string;
  veiculoId: string;
  planoId: string;
  retornoPrevistoLocal: string;
  hodometro: string;
}

const modeloVazio = (): ModeloContratacao => ({
  clienteId: '',
  condutorId: '',
  veiculoId: '',
  planoId: '',
  retornoPrevistoLocal: sugestaoRetornoDatetimeLocal(),
  hodometro: '',
});

function respostaIncerta(erro: unknown): boolean {
  if (!(erro instanceof HttpErrorResponse)) return true;
  return erro.status === 0 || erro.status === 502 || erro.status === 503 || erro.status === 504;
}

@Component({
  selector: 'app-contratacao-locacao',
  imports: [FormRoot, FormField, RouterLink, MoedaBrlPipe, ResumoErroComponent],
  templateUrl: './contratacao-locacao.component.html',
})
export class ContratacaoLocacaoComponent implements OnInit {
  readonly contratoEdicao = input<Recurso<ContratoAluguel> | null>(null);
  readonly voltar = output<void>();
  readonly concluido = output<{ id: string; revisao: number; texto: string }>();

  private readonly pessoas = inject(PessoasService);
  private readonly veiculos = inject(VeiculosService);
  private readonly grupos = inject(GruposPlanosService);
  private readonly precos = inject(PrecosService);
  private readonly alugueis = inject(AlugueisService);
  private readonly sessao = inject(SessaoService);
  private readonly brl = new FormatadorBrl();

  protected readonly carregando = signal(true);
  protected readonly errosResumo = signal<string | null>(null);
  protected readonly acaoExtra = signal<string | null>(null);
  protected readonly clientes = signal<ResumoCliente[]>([]);
  protected readonly condutores = signal<Condutor[]>([]);
  protected readonly frotaDisponivel = signal<Veiculo[]>([]);
  protected readonly catalogoGrupos = signal<Grupo[]>([]);
  protected readonly taxas = signal<TaxaServico[]>([]);
  protected readonly seguroCliente = signal<PrecoSeguro | null>(null);
  protected readonly seguroTerceiros = signal<PrecoSeguro | null>(null);
  protected readonly taxasMarcadas = signal<string[]>([]);
  protected readonly segurosMarcados = signal<('Cliente' | 'Terceiros')[]>([]);
  protected readonly estimativa = signal<EstimativaContrato | null>(null);
  protected readonly calculandoEstimativa = signal(false);
  protected readonly confirmando = signal(false);
  protected readonly incerto = signal(false);
  protected readonly congelado = computed(() => this.incerto() || this.confirmando());
  protected readonly versaoEntrada = signal(0);
  protected readonly versaoEstimativa = signal(0);
  protected readonly estimativaDesatualizada = computed(() => {
    const atual = this.estimativa();
    return atual !== null && this.versaoEntrada() !== this.versaoEstimativa();
  });
  protected readonly itensContrato = signal<ContratoAluguel['itens']>([]);
  protected readonly saidaExibicao = signal<string | null>(null);
  protected readonly erroCarga = signal(false);
  protected readonly reautenticar = signal(false);
  protected readonly errosCampos = signal<Record<string, string[]>>({});
  private atorPendente: string | null = null;
  private empresaPendente: string | null = null;
  private consultaCondutores = 0;
  private readonly baseEdicao = signal<ContratoAluguel | null>(null);

  private chaveIdempotencia: string | null = null;
  private comandoPendente: EntradaContratacao | null = null;
  private etagEdicao = '';

  protected readonly modoEdicao = computed(() => this.contratoEdicao() !== null);
  protected readonly contratoAtual = computed(() => this.baseEdicao() ?? this.contratoEdicao()?.dados ?? null);

  protected readonly rotulosModalidade: Record<ModalidadePlano, string> = {
    Diario: 'Diário', Controlado: 'Controlado', Livre: 'Livre',
  };

  protected readonly modelo = signal<ModeloContratacao>(modeloVazio());

  protected readonly clienteAtual = computed(() => this.clientes().find((c) => c.id === this.modelo().clienteId));
  protected readonly condutorAtual = computed(() => this.condutores().find((c) => c.id === this.modelo().condutorId));
  protected readonly veiculoAtual = computed(() => this.frotaDisponivel().find((v) => v.id === this.modelo().veiculoId));
  protected readonly grupoDoVeiculo = computed(() => {
    const veiculo = this.veiculoAtual();
    return veiculo ? this.catalogoGrupos().find((g) => g.id === veiculo.grupoId) : undefined;
  });
  protected readonly planosDoGrupo = computed(() => (this.grupoDoVeiculo()?.planos ?? []).map((plano) => {
    const base = this.contratoAtual();
    return base?.planoId === plano.id && base.veiculo.grupoId === this.veiculoAtual()?.grupoId
      ? { ...plano, ...base.plano, modalidade: plano.modalidade } : plano;
  }));
  protected readonly planoAtual = computed(() => this.planosDoGrupo().find((p) => p.id === this.modelo().planoId));
  protected readonly grupoNome = computed(() => this.grupoDoVeiculo()?.nome ?? '');
  protected nomeGrupo(id: string): string {
    return this.catalogoGrupos().find((grupo) => grupo.id === id)?.nome ?? '';
  }

  protected readonly formulario = form(this.modelo, (caminho) => {
    const campos: Record<keyof ModeloContratacao, string> = {
      clienteId: 'clienteId', condutorId: 'condutorId', veiculoId: 'veiculoId', planoId: 'planoId',
      retornoPrevistoLocal: 'retornoPrevisto', hodometro: 'hodometroInicial',
    };
    for (const campo of Object.keys(campos) as (keyof ModeloContratacao)[]) {
      validate(caminho[campo], () => {
        const mensagem = this.errosCampos()[campos[campo]]?.join(' ');
        return mensagem ? { kind: 'servidor', message: mensagem } : null;
      });
    }
    required(caminho.clienteId, { message: 'Selecione o cliente contratante.' });
    required(caminho.condutorId, { message: 'Selecione o condutor.' });
    required(caminho.veiculoId, { message: 'Selecione o veículo disponível.' });
    required(caminho.planoId, { message: 'Selecione um plano do grupo.' });
    required(caminho.retornoPrevistoLocal, { message: 'Informe o retorno previsto.' });
    required(caminho.hodometro, { message: 'Informe o hodômetro inicial.' });
    validate(caminho.hodometro, ({ value }) => {
      const texto = value().trim();
      if (!texto) return null;
      if (!/^\d+$/.test(texto)) return { kind: 'hodometro', message: 'Use um valor inteiro não negativo em quilômetros.' };
      return null;
    });
    validate(caminho.retornoPrevistoLocal, ({ value }) => {
      if (!value().trim()) return null;
      try {
        transporteRetornoPrevisto(value());
        return null;
      } catch {
        return { kind: 'retorno', message: 'Informe data e hora válidas em São Paulo.' };
      }
    });
  }, {
    submission: {
      onInvalid: async () => {
        this.errosResumo.set('Revise os campos indicados antes de continuar.');
        await focarResumo('erro-contratacao');
      },
      action: async () => this.confirmar(),
    },
  });

  async ngOnInit(): Promise<void> {
    this.carregando.set(true);
    this.erroCarga.set(false);
    const edicao = this.contratoEdicao();
    if (edicao) this.etagEdicao = edicao.etag;
    try {
      const [clientes, veiculos, grupos, taxas, segCli, segTer] = await Promise.all([
        this.pessoas.listarTodosClientes(),
        this.veiculos.listarDisponiveis(edicao?.dados.id),
        this.carregarGrupos(),
        this.precos.listarTodasTaxas(),
        this.precos.seguro('Cliente'),
        this.precos.seguro('Terceiros'),
      ]);
      this.clientes.set(clientes);
      this.frotaDisponivel.set(veiculos);
      this.catalogoGrupos.set(grupos);
      this.taxas.set(taxas);
      this.seguroCliente.set(segCli.dados);
      this.seguroTerceiros.set(segTer.dados);
      if (edicao) await this.preencherEdicao(edicao.dados);
    } catch (erro) {
      this.erroCarga.set(true);
      await this.exibirProblema(erro);
    } finally {
      this.carregando.set(false);
    }
  }

  @HostListener('window:beforeunload', ['$event'])
  protegerSaidaIncerta(evento: BeforeUnloadEvent): void {
    if (this.navegacaoBloqueada()) {
      evento.preventDefault();
      evento.returnValue = '';
    }
  }

  protected formatarValidadeCnh(data: string): string {
    const [ano, mes, dia] = data.split('-');
    return `${dia}/${mes}/${ano}`;
  }

  protected formatarSp = formatarInstanteSp;

  navegacaoBloqueada(): boolean {
    return this.incerto() || this.confirmando();
  }

  protected precoTaxa(id: string, atual: string): string {
    return this.itensContrato().find((i) => i.categoria === 'TaxaServico' && i.origemId === id)?.precoUnitario ?? atual;
  }

  protected precoSeguro(tipo: string, atual: string): string {
    return this.itensContrato().find((i) => i.tipoSeguro === tipo)?.precoUnitario ?? atual;
  }

  protected mascarar(documento: string): string {
    return documento.replace(/.(?=.{4})/g, '*');
  }

  protected detalhePlano(plano: Plano): string {
    if (plano.modalidade === 'Diario') return `${this.brl.transform(plano.precoKm ?? '0')} por km rodado`;
    if (plano.modalidade === 'Controlado') {
      return `${plano.franquiaDiariaKm} km/dia · ${this.brl.transform(plano.precoKmExcedente ?? '0')}/km excedente`;
    }
    return 'Quilometragem sem cobrança no plano';
  }

  protected rotuloPlanoPreservado(plano: Plano): string | null {
    if (!this.modoEdicao()) return null;
    const base = this.contratoAtual();
    if (!base) return null;
    if (base.planoId === plano.id && base.veiculo.grupoId === this.veiculoAtual()?.grupoId) {
      return 'Preço contratado preservado';
    }
    return 'Preço atual · nova seleção';
  }

  protected rotuloAdicionalPreservado(origemId: string, tipo?: 'Cliente' | 'Terceiros'): string | null {
    if (!this.modoEdicao()) return null;
    const item = this.itensContrato().find((i) =>
      tipo ? i.tipoSeguro === tipo : i.origemId === origemId,
    );
    if (!item) return 'Preço atual';
    return 'Preço preservado';
  }

  protected async alterarCliente(valor: string): Promise<void> {
    const consulta = ++this.consultaCondutores;
    this.invalidarEstimativa();
    this.modelo.update((m) => ({ ...m, clienteId: valor, condutorId: '' }));
    this.condutores.set([]);
    if (!valor) return;
    try {
      const condutores = await this.pessoas.listarCondutoresDoCliente(valor);
      if (consulta === this.consultaCondutores) this.condutores.set(condutores);
    } catch (erro) {
      if (consulta === this.consultaCondutores) await this.exibirProblema(erro);
    }
  }

  protected alterarCondutor(valor: string): void {
    this.invalidarEstimativa();
    this.modelo.update((m) => ({ ...m, condutorId: valor }));
  }

  protected alterarVeiculo(valor: string): void {
    this.invalidarEstimativa();
    this.modelo.update((m) => ({ ...m, veiculoId: valor, planoId: '' }));
  }

  protected selecionarPlano(planoId: string): void {
    this.invalidarEstimativa();
    this.modelo.update((m) => ({ ...m, planoId }));
  }

  protected alternarTaxa(id: string, marcada: boolean): void {
    this.invalidarEstimativa();
    this.taxasMarcadas.update((lista) => marcada ? [...lista, id] : lista.filter((item) => item !== id));
  }

  protected taxaMarcada(id: string): boolean {
    return this.taxasMarcadas().includes(id);
  }

  protected alternarSeguro(tipo: 'Cliente' | 'Terceiros', marcada: boolean): void {
    const config = tipo === 'Cliente' ? this.seguroCliente() : this.seguroTerceiros();
    if (!config?.configurado) return;
    this.invalidarEstimativa();
    this.segurosMarcados.update((lista) => {
      if (marcada) return lista.includes(tipo) ? lista : [...lista, tipo];
      return lista.filter((item) => item !== tipo);
    });
  }

  protected seguroMarcado(tipo: 'Cliente' | 'Terceiros'): boolean {
    return this.segurosMarcados().includes(tipo);
  }

  protected podeCalcular(): boolean {
    if (this.carregando() || this.erroCarga() || this.congelado() || this.acaoExtra() === 'recarregar') return false;
    if (!this.condutores().length && this.modelo().clienteId) return false;
    if (!this.frotaDisponivel().length) return false;
    if (this.modelo().veiculoId && !this.planosDoGrupo().length) return false;
    return this.dadosMinimosValidos();
  }

  protected podeConfirmar(): boolean {
    if (!this.podeCalcular() || this.estimativaDesatualizada() || !this.estimativa()) return false;
    return true;
  }

  protected invalido(campo: keyof ModeloContratacao): boolean {
    return this.formulario[campo]().invalid() && this.formulario[campo]().touched();
  }

  protected readonly mensagemCampo = mensagemCampo;

  protected comandoTransporte(): EntradaContratacao | null {
    try {
      const m = this.modelo();
      return {
        clienteId: m.clienteId,
        condutorId: m.condutorId,
        veiculoId: m.veiculoId,
        planoId: m.planoId,
        retornoPrevisto: transporteRetornoPrevisto(m.retornoPrevistoLocal),
        hodometroInicial: Number(m.hodometro),
        taxasServicoIds: [...this.taxasMarcadas()].sort(),
        seguros: [...this.segurosMarcados()].sort(),
      };
    } catch {
      return null;
    }
  }

  protected async calcularEstimativa(): Promise<void> {
    if (!this.podeCalcular() || this.calculandoEstimativa()) return;
    const comando = this.comandoTransporte();
    if (!comando) return;
    const versao = this.versaoEntrada();
    this.calculandoEstimativa.set(true);
    this.errosResumo.set(null);
    this.acaoExtra.set(null);
    try {
      const edicao = this.contratoEdicao();
      const resposta = await this.alugueis.estimar(
        edicao ? { ...comando, aluguelId: edicao.dados.id } : comando,
        edicao ? this.etagEdicao : undefined,
      );
      if (versao !== this.versaoEntrada()) {
        this.estimativa.set(null);
        return;
      }
      this.estimativa.set(resposta.estimativa);
      this.versaoEstimativa.set(versao);
    } catch (erro) {
      if (versao !== this.versaoEntrada()) return;
      this.estimativa.set(null);
      await this.exibirProblema(erro);
      if (respostaIncerta(erro)) {
        this.errosResumo.set('Não foi possível calcular a estimativa. Os dados foram mantidos. Tente calcular novamente; nenhum contrato foi criado pela prévia.');
      }
    } finally {
      this.calculandoEstimativa.set(false);
    }
  }

  protected async confirmar(): Promise<void> {
    if (!this.podeConfirmar() || this.incerto()) return;
    const comando = this.comandoTransporte();
    if (!comando) return;
    this.confirmando.set(true);
    this.errosResumo.set(null);
    this.acaoExtra.set(null);
    try {
      if (this.modoEdicao()) {
        const edicao = this.contratoEdicao()!;
        const resultado = await this.alugueis.alterar(edicao.dados.id, comando, this.etagEdicao);
        this.concluido.emit({
          id: resultado.dados.id,
          revisao: resultado.dados.revisao,
          texto: `Alterações salvas · ${resultado.dados.id} · revisão ${resultado.dados.revisao} · estimativa contratada de ${this.brl.transform(resultado.dados.estimativa.totalConhecido)}.`,
        });
        return;
      }
      if (!this.chaveIdempotencia) this.chaveIdempotencia = crypto.randomUUID();
      this.comandoPendente = comando;
      this.atorPendente = this.sessao.memoria.contaId();
      this.empresaPendente = this.sessao.memoria.empresaId();
      const resultado = await this.alugueis.criar(comando, this.chaveIdempotencia);
      this.incerto.set(false);
      this.comandoPendente = null;
      this.concluido.emit({
        id: resultado.dados.id,
        revisao: resultado.dados.revisao,
        texto: `Locação confirmada · ${resultado.dados.id} · Em aberto · revisão ${resultado.dados.revisao} · estimativa contratada de ${this.brl.transform(resultado.dados.estimativa.totalConhecido)}. A prévia não congelava os valores; esta é a composição registrada.`,
      });
    } catch (erro) {
      if (!this.modoEdicao() && (respostaIncerta(erro) || (erro instanceof HttpErrorResponse && [401, 403].includes(erro.status)))) {
        this.comandoPendente = comando;
        this.incerto.set(true);
        this.errosResumo.set(null);
        if (erro instanceof HttpErrorResponse && erro.status === 401) this.reautenticar.set(true);
        if (erro instanceof HttpErrorResponse && erro.status === 403) this.errosResumo.set('Acesso negado. O resultado anterior continua incerto.');
        await focarResumo('aviso-incerto-contratacao');
        return;
      }
      if (!this.modoEdicao()) {
        this.chaveIdempotencia = null;
        this.comandoPendente = null;
        this.estimativa.set(null);
      }
      if (this.modoEdicao() && respostaIncerta(erro)) {
        this.acaoExtra.set('recarregar');
        this.errosResumo.set('Não foi possível confirmar a resposta da edição. Recarregue o contrato para consultar o resultado antes de salvar novamente.');
        await focarResumo('erro-contratacao');
        return;
      }
      await this.exibirProblema(erro);
    } finally {
      this.confirmando.set(false);
    }
  }

  protected async verificarContratacao(): Promise<void> {
    if (!this.comandoPendente || !this.chaveIdempotencia || this.confirmando()) return;
    if (this.sessao.memoria.contaId() !== this.atorPendente || this.sessao.memoria.empresaId() !== this.empresaPendente) {
      this.reautenticar.set(true);
      this.errosResumo.set('Entre novamente com a mesma conta da tentativa original.');
      return;
    }
    this.confirmando.set(true);
    try {
      const resultado = await this.alugueis.criar(this.comandoPendente, this.chaveIdempotencia);
      this.incerto.set(false);
      this.comandoPendente = null;
      this.concluido.emit({
        id: resultado.dados.id,
        revisao: resultado.dados.revisao,
        texto: `Locação confirmada · ${resultado.dados.id} · Em aberto · revisão ${resultado.dados.revisao}.`,
      });
    } catch (erro) {
      if (respostaIncerta(erro)) {
        await focarResumo('aviso-incerto-contratacao');
        return;
      }
      if (erro instanceof HttpErrorResponse && erro.status === 401) this.reautenticar.set(true);
      await this.exibirProblema(erro);
    } finally {
      this.confirmando.set(false);
    }
  }

  protected async entrarNovamente(evento: Event, usuario: string, senha: HTMLInputElement): Promise<void> {
    evento.preventDefault();
    const valor = senha.value;
    senha.value = '';
    const erro = await this.sessao.entrar(usuario, valor);
    if (erro || this.sessao.memoria.contaId() !== this.atorPendente || this.sessao.memoria.empresaId() !== this.empresaPendente) {
      this.errosResumo.set(erro ?? 'Entre com a mesma conta da tentativa original.');
      return;
    }
    this.reautenticar.set(false);
    this.errosResumo.set(null);
  }

  protected async recarregarContrato(): Promise<void> {
    const edicao = this.contratoEdicao();
    if (!edicao) return;
    this.carregando.set(true);
    this.errosResumo.set(null);
    this.acaoExtra.set(null);
    try {
      const atualizado = await this.alugueis.consultar(edicao.dados.id);
      const [veiculos, grupos, clientes] = await Promise.all([
        this.veiculos.listarDisponiveis(edicao.dados.id), this.carregarGrupos(), this.pessoas.listarTodosClientes(),
      ]);
      this.frotaDisponivel.set(veiculos);
      this.catalogoGrupos.set(grupos);
      this.clientes.set(clientes);
      this.etagEdicao = atualizado.etag;
      await this.preencherEdicao(atualizado.dados);
      this.estimativa.set(atualizado.dados.estimativa);
      this.versaoEstimativa.set(this.versaoEntrada());
    } catch (erro) {
      await this.exibirProblema(erro);
    } finally {
      this.carregando.set(false);
    }
  }

  protected aoVoltar(): void {
    if (this.navegacaoBloqueada()) {
      void focarResumo('aviso-incerto-contratacao');
      return;
    }
    this.voltar.emit();
  }

  protected invalidarEstimativa(): void {
    if (this.congelado()) return;
    this.errosCampos.set({});
    this.versaoEntrada.update((v) => v + 1);
  }

  private dadosMinimosValidos(): boolean {
    const f = this.formulario;
    return ![f.clienteId, f.condutorId, f.veiculoId, f.retornoPrevistoLocal, f.hodometro].some((campo) => campo().invalid())
      && !!this.modelo().planoId;
  }

  private async preencherEdicao(contrato: ContratoAluguel): Promise<void> {
    this.baseEdicao.set(contrato);
    this.clientes.update((clientes) => clientes.map((cliente) => cliente.id === contrato.clienteId
      ? { ...cliente, nome: contrato.cliente.nome } : cliente));
    this.catalogoGrupos.update((grupos) => grupos.map((grupo) => grupo.id === contrato.veiculo.grupoId
      ? { ...grupo, nome: contrato.veiculo.grupoNome } : grupo));
    this.itensContrato.set(contrato.itens);
    this.saidaExibicao.set(formatarInstanteSp(contrato.saidaUtc));
    this.condutores.set((await this.pessoas.listarCondutoresDoCliente(contrato.clienteId)).map((condutor) =>
      condutor.id === contrato.condutorId ? { ...condutor, nome: contrato.condutor.nome } : condutor));
    const taxasIds = contrato.itens.filter((i) => i.categoria === 'TaxaServico').map((i) => i.origemId);
    const seguros = contrato.itens
      .filter((i) => i.categoria === 'Seguro' && i.tipoSeguro)
      .map((i) => i.tipoSeguro as 'Cliente' | 'Terceiros');
    this.taxasMarcadas.set(taxasIds);
    this.segurosMarcados.set(seguros);
    this.modelo.set({
      clienteId: contrato.clienteId,
      condutorId: contrato.condutorId,
      veiculoId: contrato.veiculoId,
      planoId: contrato.planoId,
      retornoPrevistoLocal: datetimeLocalSaoPaulo(contrato.retornoPrevistoUtc),
      hodometro: String(contrato.hodometroInicial),
    });
    this.estimativa.set(contrato.estimativa);
    this.versaoEstimativa.set(this.versaoEntrada());
  }

  private async exibirProblema(erro: unknown): Promise<void> {
    const problema: ProblemaCadastro = problemaCadastro(erro);
    if (erro instanceof HttpErrorResponse && erro.status === 412 && this.modoEdicao()) {
      this.errosResumo.set('Este contrato foi atualizado por outra operação. Recarregue antes de salvar; seus ajustes não foram gravados.');
      this.acaoExtra.set('recarregar');
      await focarResumo('erro-contratacao');
      return;
    }
    const mensagens = problema.errors
      ? Object.values(problema.errors).flat()
      : [];
    this.errosCampos.set(problema.errors ?? {});
    for (const campo of Object.keys(this.modelo()) as (keyof ModeloContratacao)[]) this.formulario[campo]().markAsTouched();
    const texto = mensagens.length
      ? `Não foi possível continuar. ${mensagens.join(' ')}`
      : (problema.title ?? 'Não foi possível continuar.');
    this.errosResumo.set(texto);
    await focarResumo('erro-contratacao');
  }

  private async carregarGrupos(): Promise<Grupo[]> {
    const grupos: Grupo[] = [];
    let pagina = 1;
    for (;;) {
      const resposta = await this.grupos.listarGrupos(pagina++);
      grupos.push(...resposta.items);
      if (!resposta.items.length || resposta.page * resposta.pageSize >= resposta.total) return grupos;
    }
  }
}
