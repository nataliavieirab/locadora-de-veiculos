import { Component, ElementRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { ShellCadastralComponent } from '../../shared/shell-cadastral.component';
import { conterTabulacao, focarResumo } from '../../shared/foco';
import { formatarInstanteSp, transporteFimExclusivoDia, transporteInicioDia } from '../../shared/horario-sao-paulo';
import { MoedaBrlPipe } from '../../shared/moeda-brl.pipe';
import { problemaCadastro } from '../../shared/erro-cadastro';
import { ResumoErroComponent } from '../../shared/resumo-erro.component';
import { Pagina } from '../frota/grupos-planos.service';
import { AlugueisService, ContratoAluguel, FiltroAlugueis, ItemListaAluguel, ReferenciaVeiculoHistorico } from './alugueis.service';
import { Recurso } from '../frota/grupos-planos.service';
import { ContratacaoLocacaoComponent } from './contratacao-locacao.component';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ApuracaoDevolucaoComponent } from './apuracao-devolucao.component';

const vazioPagina = (): Pagina<ItemListaAluguel> => ({ items: [], page: 1, pageSize: 20, total: 0 });

@Component({
  selector: 'app-alugueis',
  imports: [ShellCadastralComponent, MoedaBrlPipe, ResumoErroComponent, ContratacaoLocacaoComponent, ApuracaoDevolucaoComponent],
  templateUrl: './alugueis.component.html',
})
export class AlugueisComponent implements OnInit {
  private readonly api = inject(AlugueisService);
  private filtrosAtivos: FiltroAlugueis = {};
  private readonly roteador = inject(Router);
  private readonly rota = inject(ActivatedRoute);
  private readonly avisoNavegacao = this.roteador.currentNavigation()?.extras.info;
  private concluidoNaPagina = false;
  private readonly consultaDialog = viewChild<ElementRef<HTMLDialogElement>>('consulta');
  private readonly ocultacaoDialog = viewChild<ElementRef<HTMLDialogElement>>('ocultacao');
  private readonly formulario = viewChild(ContratacaoLocacaoComponent);
  private consulta = 0;
  private abertura = 0;
  private origem: HTMLElement | null = null;
  private origemOcultacao: HTMLElement | null = null;
  private ultimaConsulta: ItemListaAluguel | null = null;
  private leituraOcultacao = 0;
  private versaoOcultacao: string | null = null;
  protected readonly alvoOcultacao = signal<ItemListaAluguel | null>(null);
  protected readonly lendoOcultacao = signal(false);
  protected readonly ocultando = signal(false);
  protected readonly erroOcultacao = signal<string | null>(null);
  protected readonly verificarOcultacao = signal(false);

  protected readonly pagina = signal(vazioPagina());
  protected readonly frota = signal<ReferenciaVeiculoHistorico[]>([]);
  protected readonly carregando = signal(true);
  protected readonly erroLista = signal<string | null>(null);
  protected readonly erroConsulta = signal<string | null>(null);
  protected readonly carregandoConsulta = signal(false);
  protected readonly contrato = signal<ContratoAluguel | null>(null);
  protected readonly aviso = signal<string | null>(null);
  protected readonly contratando = signal(false);
  protected readonly contratoEdicao = signal<Recurso<ContratoAluguel> | null>(null);
  protected readonly carregandoEdicao = signal(false);
  protected readonly periodoInvalido = signal(false);

  protected readonly filtroEstado = signal('');
  protected readonly filtroVeiculo = signal('');
  protected readonly filtroSaidaDe = signal('');
  protected readonly filtroSaidaAte = signal('');
  protected readonly filtrosAplicados = signal(false);
  protected readonly incluirOcultos = signal(false);

  protected readonly rotuloModalidade: Record<string, string> = {
    Diario: 'Diário', Controlado: 'Controlado', Livre: 'Livre', EmAberto: 'Em aberto',
  };

  async ngOnInit(): Promise<void> {
    if (this.rota.snapshot.data['nova']) {
      this.contratando.set(true);
      this.carregando.set(false);
      await focarResumo('titulo-contratacao');
      return;
    }
    const id = this.rota.snapshot.paramMap.get('id');
    if (id) {
      await this.carregarEdicao(id);
      this.carregando.set(false);
      return;
    }
    if (typeof this.avisoNavegacao === 'string') this.aviso.set(this.avisoNavegacao);
    await this.carregar();
    await focarResumo(this.aviso() ? 'aviso-alugueis' : 'titulo-alugueis');
  }

  protected rotuloEstado(estado: string): string {
    return estado === 'EmAberto' ? 'Em aberto' : estado === 'Concluido' ? 'Concluído' : estado;
  }

  protected formatarSp = formatarInstanteSp;
  protected mascarar(documento: string | null): string {
    return documento?.replace(/.(?=.{4})/g, '*') ?? '—';
  }
  protected formatarData(data: string): string {
    return data.split('-').reverse().join('/');
  }
  protected formatarLitros(litros: string): string {
    return litros.replace('.', ',');
  }

  podeSair(): boolean {
    return !this.ocultando() && (this.concluidoNaPagina || !this.formulario()?.navegacaoBloqueada());
  }

  protected async carregar(page = 1): Promise<void> {
    const id = ++this.consulta;
    this.carregando.set(true);
    this.erroLista.set(null);
    try {
      const filtro = this.filtrosAtivos;
      const [pagina, referencias] = await Promise.all([
        this.api.listar(page, 20, filtro), this.api.todasReferenciasVeiculos(filtro.incluirOcultos ?? false),
      ]);
      if (id !== this.consulta) return;
      this.pagina.set(pagina);
      this.frota.set(referencias);
      this.filtrosAplicados.set(!!filtro.estado || !!filtro.veiculoId || !!filtro.saidaDe || !!filtro.saidaAte || !!filtro.incluirOcultos);
    } catch (erro) {
      if (id !== this.consulta) return;
      this.erroLista.set(problemaCadastro(erro).title!);
      await focarResumo('erro-lista-alugueis');
    } finally {
      if (id === this.consulta) this.carregando.set(false);
    }
  }

  protected async aplicarFiltros(evento: Event): Promise<void> {
    evento.preventDefault();
    const de = this.filtroSaidaDe();
    const ate = this.filtroSaidaAte();
    if (de && ate && de > ate) {
      this.periodoInvalido.set(true);
      document.getElementById('filtro-saida-ate')?.focus();
      return;
    }
    this.periodoInvalido.set(false);
    this.filtrosAtivos = this.montarFiltroApi();
    await this.carregar();
  }

  protected async limparFiltros(): Promise<void> {
    this.filtroEstado.set('');
    this.filtroVeiculo.set('');
    this.filtroSaidaDe.set('');
    this.filtroSaidaAte.set('');
    this.incluirOcultos.set(false);
    this.filtrosAtivos = {};
    this.periodoInvalido.set(false);
    await this.carregar();
  }

  protected atualizaEstado(evento: Event): void {
    this.filtroEstado.set((evento.target as HTMLSelectElement).value);
  }

  protected atualizaVeiculo(evento: Event): void {
    this.filtroVeiculo.set((evento.target as HTMLSelectElement).value);
  }

  protected atualizaSaidaDe(evento: Event): void {
    this.filtroSaidaDe.set((evento.target as HTMLInputElement).value);
    if (this.periodoInvalido()) this.periodoInvalido.set(false);
  }

  protected atualizaSaidaAte(evento: Event): void {
    this.filtroSaidaAte.set((evento.target as HTMLInputElement).value);
    if (this.periodoInvalido()) this.periodoInvalido.set(false);
  }

  protected abrirContratacao(evento?: Event): void {
    void this.roteador.navigateByUrl('/alugueis/nova');
  }

  protected voltarLista(): void {
    void this.roteador.navigateByUrl('/alugueis');
  }

  protected async abrirEdicao(item: ItemListaAluguel, evento: Event): Promise<void> {
    await this.roteador.navigateByUrl(`/alugueis/${item.id}/editar`);
  }

  protected async abrirDevolucao(id: string): Promise<void> {
    this.fecharConsulta();
    await this.roteador.navigateByUrl(`/alugueis/${id}/devolucao`);
  }

  private async carregarEdicao(id: string): Promise<void> {
    this.aviso.set(null);
    this.erroLista.set(null);
    this.carregandoEdicao.set(true);
    try {
      const contrato = await this.api.consultar(id);
      if (contrato.dados.estado !== 'EmAberto') {
        this.aviso.set('Este contrato não está Em aberto e não admite edição.');
        document.getElementById('aviso-alugueis')?.focus();
        return;
      }
      this.contratoEdicao.set(contrato);
      this.contratando.set(true);
      await focarResumo('titulo-contratacao');
    } catch (erro) {
      this.erroLista.set(problemaCadastro(erro).title!);
      await focarResumo('erro-lista-alugueis');
    } finally {
      this.carregandoEdicao.set(false);
    }
  }

  protected async abrirEdicaoConsulta(): Promise<void> {
    const atual = this.contrato();
    if (!atual) return;
    this.fecharConsulta();
    await this.abrirEdicao({
      id: atual.id,
      estado: atual.estado,
      revisao: atual.revisao,
      condutorNome: atual.condutor.nome,
      condutorCpfResumo: '',
      veiculoPlaca: atual.veiculo.placa,
      planoModalidade: atual.plano.modalidade,
      saidaUtc: atual.saidaUtc,
      retornoPrevistoUtc: atual.retornoPrevistoUtc,
      totalConhecido: atual.estimativa.totalConhecido,
    }, { currentTarget: document.getElementById('fechar-consulta') } as Event);
  }

  protected async formularioConcluido(evento: { id: string; revisao: number; texto: string }): Promise<void> {
    this.concluidoNaPagina = true;
    await this.roteador.navigateByUrl('/alugueis', { info: evento.texto });
  }

  protected async abrirConsulta(item: ItemListaAluguel, evento: Event): Promise<void> {
    this.ultimaConsulta = item;
    const abertura = ++this.abertura;
    const alvo = evento.currentTarget;
    this.origem = alvo instanceof HTMLElement ? alvo : null;
    this.erroConsulta.set(null);
    this.contrato.set(null);
    this.carregandoConsulta.set(true);
    this.consultaDialog()?.nativeElement.showModal();
    try {
      const resultado = await this.api.consultar(item.id, this.filtrosAtivos.incluirOcultos ?? false);
      if (abertura !== this.abertura) return;
      this.contrato.set(resultado.dados);
      await new Promise((resolve) => setTimeout(resolve));
      if (abertura === this.abertura) {
        this.consultaDialog()?.nativeElement.querySelector<HTMLButtonElement>('#fechar-consulta')?.focus();
      }
    } catch (erro) {
      if (abertura === this.abertura) {
        this.erroConsulta.set(problemaCadastro(erro).title!);
        await focarResumo('erro-consulta-aluguel');
      }
    } finally {
      if (abertura === this.abertura) this.carregandoConsulta.set(false);
    }
  }

  protected repetirConsulta(): void {
    if (this.ultimaConsulta) void this.abrirConsulta(this.ultimaConsulta, { currentTarget: this.origem } as unknown as Event);
  }

  protected async pedirOcultacao(item: ItemListaAluguel, evento: Event): Promise<void> {
    if (item.estado !== 'Concluido' || item.oculto || this.ocultando()) return;
    const leitura = ++this.leituraOcultacao;
    this.origemOcultacao = evento.currentTarget instanceof HTMLElement ? evento.currentTarget : this.origem;
    this.alvoOcultacao.set(item);
    this.erroOcultacao.set(null);
    this.verificarOcultacao.set(false);
    this.versaoOcultacao = null;
    this.lendoOcultacao.set(true);
    this.consultaDialog()?.nativeElement.close();
    this.ocultacaoDialog()?.nativeElement.showModal();
    this.ocultacaoDialog()?.nativeElement.querySelector<HTMLButtonElement>('#voltar-sem-ocultar')?.focus();
    try {
      const recurso = await this.api.consultar(item.id);
      if (leitura !== this.leituraOcultacao) return;
      this.versaoOcultacao = recurso.etag;
    } catch (erro) {
      if (leitura !== this.leituraOcultacao) return;
      this.erroOcultacao.set(problemaCadastro(erro).title!);
      this.verificarOcultacao.set(true);
      await focarResumo('erro-ocultacao');
    } finally {
      if (leitura === this.leituraOcultacao) this.lendoOcultacao.set(false);
    }
  }

  protected pedirOcultacaoConsulta(evento: Event): void {
    const c = this.contrato();
    if (!c || c.estado !== 'Concluido' || c.oculto) return;
    void this.pedirOcultacao({ id: c.id, estado: c.estado, revisao: c.revisao,
      condutorNome: c.condutor.nome, condutorCpfResumo: '', veiculoPlaca: c.veiculo.placa,
      planoModalidade: c.plano.modalidade, saidaUtc: c.saidaUtc, retornoPrevistoUtc: c.retornoPrevistoUtc,
      totalConhecido: c.estimativa.totalConhecido, totalFinal: c.devolucao?.calculo.totalFinal, oculto: c.oculto }, evento);
  }

  protected cancelarOcultacao(evento?: Event): void {
    if (this.ocultando()) { evento?.preventDefault(); return; }
    evento?.preventDefault();
    this.leituraOcultacao++;
    this.ocultacaoDialog()?.nativeElement.close();
  }

  protected ocultacaoFechada(): void {
    if (this.origemOcultacao?.isConnected && !this.origemOcultacao.closest('dialog:not([open])'))
      this.origemOcultacao.focus();
    else if (this.origem?.isConnected) this.origem.focus();
    else document.getElementById('titulo-alugueis')?.focus();
  }

  protected teclaOcultacao(evento: KeyboardEvent): void {
    const dialogo = this.ocultacaoDialog()?.nativeElement;
    if (dialogo) conterTabulacao(evento, dialogo);
  }

  protected async confirmarOcultacao(): Promise<void> {
    const item = this.alvoOcultacao();
    if (!item || !this.versaoOcultacao || this.lendoOcultacao() || this.ocultando() || this.verificarOcultacao()) return;
    this.ocultando.set(true);
    try {
      await this.api.ocultar(item.id, this.versaoOcultacao);
      this.ocultacaoDialog()?.nativeElement.close();
      this.contrato.set(null);
      this.aviso.set('Locação ocultada da operação. O contrato e o demonstrativo final foram preservados.');
      await this.carregar();
      await focarResumo('titulo-alugueis');
    } catch (erro) {
      const status = erro instanceof HttpErrorResponse ? erro.status : 0;
      this.verificarOcultacao.set(true);
      this.erroOcultacao.set(status === 412 || status === 428
        ? 'O contrato foi atualizado ou está sem versão. Recarregue a consulta antes de tentar novamente.'
        : 'Resultado da ocultação não confirmado. Atualize a consulta para verificar o estado; nenhum sucesso foi confirmado.');
      await focarResumo('erro-ocultacao');
    } finally {
      this.ocultando.set(false);
    }
  }

  protected async atualizarAposOcultacao(): Promise<void> {
    if (this.ocultando()) return;
    this.ocultacaoDialog()?.nativeElement.close();
    await this.carregar();
    await focarResumo('titulo-alugueis');
  }

  protected fecharConsulta(): void {
    this.abertura++;
    this.consultaDialog()?.nativeElement.close();
  }

  protected consultaFechada(): void {
    if (this.origem?.isConnected) this.origem.focus();
    else document.getElementById('titulo-alugueis')?.focus();
  }

  protected teclaConsulta(evento: KeyboardEvent): void {
    const dialogo = this.consultaDialog()?.nativeElement;
    if (dialogo) conterTabulacao(evento, dialogo);
  }

  protected cancelarConsulta(_evento: Event): void {
    this.abertura++;
  }

  private montarFiltroApi() {
    const estado = this.filtroEstado();
    const veiculoId = this.filtroVeiculo();
    const saidaDe = this.filtroSaidaDe();
    const saidaAte = this.filtroSaidaAte();
    return {
      ...(estado ? { estado } : {}),
      ...(veiculoId ? { veiculoId } : {}),
      ...(saidaDe ? { saidaDe: transporteInicioDia(saidaDe) } : {}),
      ...(saidaAte ? { saidaAte: transporteFimExclusivoDia(saidaAte) } : {}),
      ...(this.incluirOcultos() ? { incluirOcultos: true } : {}),
    };
  }
}
