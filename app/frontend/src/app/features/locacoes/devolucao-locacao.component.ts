import { Component, computed, effect, ElementRef, HostListener, inject, OnInit, signal, untracked, viewChild } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { disabled, form, FormField, required, validate } from '@angular/forms/signals';
import { ShellCadastralComponent } from '../../shared/shell-cadastral.component';
import { MoedaBrlPipe } from '../../shared/moeda-brl.pipe';
import { ResumoErroComponent } from '../../shared/resumo-erro.component';
import { conterTabulacao, focarResumo } from '../../shared/foco';
import { formatarInstanteSp, transporteRetornoPrevisto } from '../../shared/horario-sao-paulo';
import { problemaCadastro } from '../../shared/erro-cadastro';
import { mensagemCampo } from '../acesso/acesso-formulario';
import { PrecosService, TaxaServico } from '../precos/precos.service';
import { Recurso } from '../frota/grupos-planos.service';
import { AlugueisService, ContratoAluguel } from './alugueis.service';
import { EntradaConclusaoDevolucao, EntradaDevolucao, PreviaDevolucao, ResultadoDevolucao } from './devolucao.contratos';
import { ApuracaoDevolucaoComponent } from './apuracao-devolucao.component';
import { SessaoService } from '../../core/sessao.service';

interface ModeloDevolucao { retornoLocal: string; hodometroFinal: string; litrosRestantes: string }

/** Compara quantidades com precisão de milésimos, sem ponto flutuante financeiro. */
function milesimos(texto: string): bigint | null {
  if (!/^\d+(?:[,.]\d{1,3})?$/.test(texto)) return null;
  const [inteiro, fracao = ''] = texto.replace(',', '.').split('.');
  if (inteiro.length > 9) return null;
  return BigInt(inteiro) * 1000n + BigInt(fracao.padEnd(3, '0'));
}

@Component({
  selector: 'app-devolucao-locacao',
  imports: [ShellCadastralComponent, RouterLink, FormField, MoedaBrlPipe, ResumoErroComponent, ApuracaoDevolucaoComponent, NgTemplateOutlet],
  templateUrl: './devolucao-locacao.component.html',
})
export class DevolucaoLocacaoComponent implements OnInit {
  private readonly api = inject(AlugueisService);
  private readonly precos = inject(PrecosService);
  private readonly rota = inject(ActivatedRoute);
  private readonly sessao = inject(SessaoService);
  private leitura = 0;
  private readonly consultaFinal = viewChild<ElementRef<HTMLDialogElement>>('consultaFinal');
  protected readonly recurso = signal<Recurso<ContratoAluguel> | null>(null);
  protected readonly carregando = signal(true);
  protected readonly erroCarga = signal(false);
  protected readonly erro = signal<string | null>(null);
  protected readonly taxas = signal<TaxaServico[]>([]);
  protected readonly taxasMarcadas = signal<string[]>([]);
  protected readonly calculando = signal(false);
  protected readonly enviando = signal(false);
  protected readonly incerto = signal(false);
  protected readonly congelado = computed(() => this.calculando() || this.enviando() || this.incerto());
  protected readonly previa = signal<PreviaDevolucao | null>(null);
  protected readonly resultado = signal<ResultadoDevolucao | null>(null);
  protected readonly exigirRecarga = signal(false);
  protected readonly reautenticar = signal(false);
  protected readonly previaDesatualizada = signal(false);
  protected readonly erroCampos = signal<Record<string, string[]>>({});
  private numeroPrevia = 0;
  private intencao: { comando: EntradaConclusaoDevolucao; etag: string; chave: string; ator: string | null; empresa: string | null } | null = null;
  protected readonly modelo = signal<ModeloDevolucao>({ retornoLocal: '', hodometroFinal: '', litrosRestantes: '' });
  protected readonly contrato = computed(() => this.recurso()?.dados ?? null);
  protected readonly aberto = computed(() => this.contrato()?.estado === 'EmAberto' && !this.resultado());
  protected readonly finalAtual = computed(() => this.resultado()?.devolucao ?? this.contrato()?.devolucao ?? null);
  protected readonly disponiveis = computed(() => {
    const contratadas = new Set(this.contrato()?.itens.filter(i => i.categoria === 'TaxaServico').map(i => i.origemId));
    return this.taxas().filter(t => !contratadas.has(t.id));
  });
  protected readonly formatarSp = formatarInstanteSp;
  protected readonly mensagemCampo = mensagemCampo;
  protected readonly rotulos: Record<string, string> = { Diario: 'Diário', Controlado: 'Controlado', Livre: 'Livre' };

  protected readonly formulario = form(this.modelo, p => {
    disabled(p, { when: () => this.congelado() || this.carregando() || !this.aberto() });
    required(p.retornoLocal, { message: 'Informe a data e hora real da devolução.' });
    required(p.hodometroFinal, { message: 'Informe o hodômetro final.' });
    required(p.litrosRestantes, { message: 'Informe os litros restantes.' });
    for (const [campo, nome] of [[p.retornoLocal, 'retornoReal'], [p.hodometroFinal, 'hodometroFinal'], [p.litrosRestantes, 'litrosRestantes']] as const)
      validate(campo, () => {
        const mensagem = this.erroCampos()[nome]?.join(' ');
        return mensagem ? { kind: 'servidor', message: mensagem } : null;
      });
    validate(p.retornoLocal, ({ value }) => {
      if (!value()) return null;
      try {
        const retorno = transporteRetornoPrevisto(value());
        if (this.contrato() && Date.parse(retorno) < Date.parse(this.contrato()!.saidaUtc))
          return { kind: 'retorno', message: 'O retorno real não pode preceder a saída.' };
        return null;
      } catch {
        return { kind: 'retorno', message: 'Informe horário válido e não ambíguo em São Paulo.' };
      }
    });
    validate(p.hodometroFinal, ({ value }) => {
      if (!value()) return null;
      const texto = value().trim();
      const n = Number(texto);
      return !/^\d+$/.test(texto) || !Number.isSafeInteger(n) || n > 2147483647 || n < (this.contrato()?.hodometroInicial ?? 0)
        ? { kind: 'hodometro', message: 'Informe quilômetros inteiros entre o hodômetro inicial e 2.147.483.647.' } : null;
    });
    validate(p.litrosRestantes, ({ value }) => {
      if (!value()) return null;
      const litros = milesimos(value().trim());
      const capacidade = milesimos(this.contrato()?.veiculo.capacidadeLitros ?? '0');
      return litros === null || capacidade === null || litros > capacidade
        ? { kind: 'litros', message: 'Informe de zero à capacidade contratada, com até três casas decimais.' } : null;
    });
  });

  constructor() {
    effect(() => {
      this.modelo();
      this.taxasMarcadas();
      untracked(() => {
        this.numeroPrevia++;
        if (this.previa()) this.previaDesatualizada.set(true);
        this.previa.set(null);
        this.erroCampos.set({});
      });
    });
  }

  async ngOnInit(): Promise<void> { await this.carregar(); }

  protected async carregar(): Promise<void> {
    if (this.navegacaoBloqueada()) return;
    const leitura = ++this.leitura;
    this.numeroPrevia++;
    this.previa.set(null);
    this.exigirRecarga.set(false);
    this.carregando.set(true);
    this.erroCarga.set(false);
    this.erro.set(null);
    this.recurso.set(null);
    this.taxas.set([]);
    this.taxasMarcadas.set([]);
    try {
      const id = this.rota.snapshot.paramMap.get('id')!;
      const [recurso, taxas] = await Promise.all([this.api.consultar(id), this.precos.listarTodasTaxas()]);
      if (leitura !== this.leitura) return;
      this.recurso.set(recurso);
      this.taxas.set(taxas);
      await focarResumo('titulo-devolucao');
    } catch (erro) {
      if (leitura !== this.leitura) return;
      this.erroCarga.set(true);
      this.erro.set(problemaCadastro(erro).title!);
      await focarResumo('erro-devolucao');
    } finally {
      if (leitura === this.leitura) this.carregando.set(false);
    }
  }

  protected async calcular(): Promise<void> {
    if (this.congelado() || this.exigirRecarga() || this.resultado()) return;
    const comando = await this.validarEntradas();
    const recurso = this.recurso();
    if (!comando || !recurso || this.congelado()) return;
    const numero = ++this.numeroPrevia;
    this.previa.set(null);
    this.erro.set(null);
    this.calculando.set(true);
    try {
      const previa = await this.api.previaDevolucao(recurso.dados.id, comando, recurso.etag);
      if (numero !== this.numeroPrevia || recurso !== this.recurso()) return;
      this.previa.set(previa);
      this.previaDesatualizada.set(false);
    } catch (erro) {
      if (numero === this.numeroPrevia) await this.exibirProblema(erro);
    } finally {
      this.calculando.set(false);
    }
  }

  protected async confirmar(evento: Event): Promise<void> {
    evento.preventDefault();
    const previa = this.previa();
    const recurso = this.recurso();
    if (!previa || !recurso || this.congelado() || this.exigirRecarga() || this.resultado()) return;
    const comando = await this.validarEntradas();
    if (!comando || this.previa() !== previa || this.congelado() || this.resultado()) return;
    this.intencao = {
      comando: structuredClone({ ...comando, versoesAceitas: previa.versoesAceitas }),
      etag: recurso.etag, chave: crypto.randomUUID(),
      ator: this.sessao.memoria.contaId(), empresa: this.sessao.memoria.empresaId(),
    };
    await this.enviarIntencao();
  }

  protected async verificar(): Promise<void> {
    if (!this.intencao || this.enviando()) return;
    if (!this.intencao.ator || this.sessao.memoria.contaId() !== this.intencao.ator
      || this.sessao.memoria.empresaId() !== this.intencao.empresa) {
      this.reautenticar.set(true);
      this.erro.set('Reautentique com a mesma conta da tentativa original.');
      return;
    }
    await this.enviarIntencao();
  }

  private async enviarIntencao(): Promise<void> {
    const intencao = this.intencao;
    if (!intencao || this.enviando()) return;
    const jaIncerto = this.incerto();
    this.enviando.set(true);
    this.erro.set(null);
    try {
      const resultado = await this.api.devolver(this.recurso()!.dados.id, intencao.comando, intencao.etag, intencao.chave);
      this.resultado.set(resultado.dados);
      this.incerto.set(false);
      this.intencao = null;
      this.previa.set(null);
      this.reautenticar.set(false);
      await focarResumo('sucesso-devolucao');
    } catch (erro) {
      const status = erro instanceof HttpErrorResponse ? erro.status : 0;
      if (jaIncerto || status === 0 || status >= 500 || status === 401 || status === 403) {
        this.incerto.set(true);
        if (status === 401) this.reautenticar.set(true);
        this.erro.set(status === 403 ? 'Acesso negado. O resultado anterior continua incerto.' : null);
        await focarResumo('incerto-devolucao');
      } else {
        this.intencao = null;
        this.previa.set(null);
        await this.exibirProblema(erro);
      }
    } finally {
      this.enviando.set(false);
    }
  }

  protected async entrarNovamente(evento: Event, usuario: HTMLInputElement, senha: HTMLInputElement): Promise<void> {
    evento.preventDefault();
    const segredo = senha.value;
    senha.value = '';
    const erro = await this.sessao.entrar(usuario.value, segredo);
    usuario.value = '';
    if (erro || !this.intencao || this.sessao.memoria.contaId() !== this.intencao.ator
      || this.sessao.memoria.empresaId() !== this.intencao.empresa) {
      this.erro.set(erro ?? 'Entre com a mesma conta da tentativa original.');
      await focarResumo('erro-devolucao');
      return;
    }
    this.reautenticar.set(false);
    this.erro.set(null);
  }

  private async exibirProblema(erro: unknown): Promise<void> {
    const problema = problemaCadastro(erro);
    const status = erro instanceof HttpErrorResponse ? erro.status : 0;
    if (status === 412 || status === 428) {
      this.exigirRecarga.set(true);
      this.erro.set('O contrato foi atualizado ou está sem versão. Recarregue o contrato antes de calcular novamente; sua intenção continua visível.');
    } else if (problema.code === 'combustivel_sem_vigencia') {
      this.erro.set('Não há preço do combustível contratado vigente na data e hora do retorno. Confira Preços e serviços; cadastrar agora não preenche retroativamente a lacuna.');
    } else if (problema.code === 'previa_desatualizada') {
      this.previaDesatualizada.set(true);
      this.erro.set('Os preços ou as entradas da prévia foram alterados. Calcule uma nova prévia antes de confirmar.');
    } else {
      this.erroCampos.set(problema.errors ?? {});
      this.erro.set(status === 0 || status >= 500
        ? 'Não foi possível calcular a prévia. Os dados foram mantidos; tente novamente. Nenhuma conclusão foi registrada pelo cálculo.'
        : problema.title ?? 'Não foi possível continuar.');
    }
    const trace = erro instanceof HttpErrorResponse ? erro.error?.traceId : null;
    if (typeof trace === 'string' && /^[a-zA-Z0-9:._-]{1,128}$/.test(trace))
      this.erro.update(texto => `${texto} Correlação: ${trace}.`);
    await focarResumo('erro-devolucao');
  }

  protected bloquearVolta(evento: Event): void {
    if (!this.navegacaoBloqueada()) return;
    evento.preventDefault();
    void focarResumo('incerto-devolucao');
  }

  protected abrirConsultaFinal(): void {
    this.consultaFinal()?.nativeElement.showModal();
    this.consultaFinal()?.nativeElement.querySelector<HTMLButtonElement>('button')?.focus();
  }

  protected fecharConsultaFinal(): void { this.consultaFinal()?.nativeElement.close(); }
  protected consultaFinalFechada(): void { document.getElementById('consultar-final')?.focus(); }
  protected teclaConsultaFinal(evento: KeyboardEvent): void {
    const dialogo = this.consultaFinal()?.nativeElement;
    if (dialogo) conterTabulacao(evento, dialogo);
  }

  @HostListener('window:beforeunload', ['$event'])
  protegerRecarga(evento: BeforeUnloadEvent): void {
    if (!this.navegacaoBloqueada()) return;
    evento.preventDefault();
    evento.returnValue = '';
  }

  protected alternarTaxa(id: string, marcada: boolean): void {
    if (this.congelado() || !this.disponiveis().some(t => t.id === id)) return;
    this.taxasMarcadas.update(ids => marcada ? [...new Set([...ids, id])] : ids.filter(i => i !== id));
  }

  protected mascarar(documento: string | null): string { return documento?.replace(/.(?=.{4})/g, '*') ?? '—'; }
  protected litros(texto: string): string { return texto.replace('.', ','); }

  protected async validarEntradas(): Promise<EntradaDevolucao | null> {
    if (!this.aberto() || this.carregando() || this.congelado()) return null;
    for (const campo of [this.formulario.retornoLocal, this.formulario.hodometroFinal, this.formulario.litrosRestantes])
      campo().markAsTouched();
    if (this.formulario().invalid()) {
      this.erro.set('Revise os campos indicados antes de calcular a prévia.');
      await focarResumo('erro-devolucao');
      return null;
    }
    const m = this.modelo();
    return {
      retornoReal: transporteRetornoPrevisto(m.retornoLocal),
      hodometroFinal: Number(m.hodometroFinal.trim()),
      litrosRestantes: m.litrosRestantes.trim().replace(',', '.'),
      taxasAdicionaisIds: [...this.taxasMarcadas()].sort(),
    };
  }

  navegacaoBloqueada(): boolean { return this.enviando() || this.incerto(); }
}
