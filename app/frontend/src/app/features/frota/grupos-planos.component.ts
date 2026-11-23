import { Component, computed, ElementRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { form, FormRoot, maxLength, required, validate } from '@angular/forms/signals';
import { ShellCadastralComponent } from '../../shared/shell-cadastral.component';
import { CampoTextoComponent } from '../../shared/campo-texto.component';
import { problemaCadastro } from '../../shared/erro-cadastro';
import { conterTabulacao, focarResumo } from '../../shared/foco';
import { MoedaBrlPipe } from '../../shared/moeda-brl.pipe';
import { ResumoErroComponent } from '../../shared/resumo-erro.component';
import { Grupo, GruposPlanosService, ModalidadePlano, Pagina, Plano, Recurso } from './grupos-planos.service';

@Component({
  selector: 'app-grupos-planos',
  imports: [FormRoot, ShellCadastralComponent, CampoTextoComponent, MoedaBrlPipe, ResumoErroComponent],
  templateUrl: './grupos-planos.component.html',
})
export class GruposPlanosComponent implements OnInit {
  private readonly api = inject(GruposPlanosService);
  private readonly editor = viewChild<ElementRef<HTMLDialogElement>>('editor');
  private grupoAtual: Recurso<Grupo> | null = null;
  private planoAtual: Recurso<Plano> | null = null;
  private grupoDoPlano: Grupo | null = null;
  private abertura = 0;
  private origemFoco: HTMLElement | null = null;

  protected readonly aba = signal<'grupos' | 'planos'>('grupos');
  protected readonly pagina = signal<Pagina<Grupo>>({ items: [], page: 1, pageSize: 20, total: 0 });
  protected readonly carregando = signal(true);
  protected readonly erroPagina = signal<string | null>(null);
  protected readonly aviso = signal<string | null>(null);
  protected readonly selecionado = signal('');
  protected readonly grupoSelecionado = computed(() => this.pagina().items.find((grupo) => grupo.id === this.selecionado()));
  protected readonly editorTipo = signal<'grupo' | 'plano'>('grupo');
  protected readonly tituloEditor = signal('Novo grupo');
  protected readonly erroEditor = signal<string | null>(null);
  protected readonly editorDisponivel = signal(false);
  protected readonly carregandoEditor = signal(false);
  protected readonly modalidade = signal<ModalidadePlano>('Diario');
  protected readonly modalidades: ModalidadePlano[] = ['Diario', 'Controlado', 'Livre'];
  protected readonly rotulos: Record<ModalidadePlano, string> = { Diario: 'Diário', Controlado: 'Controlado', Livre: 'Livre' };
  private readonly modeloGrupo = signal({ nome: '' });
  private readonly modeloPlano = signal({ precoDiario: '', precoKm: '', franquiaDiariaKm: '', precoKmExcedente: '' });

  protected readonly formularioGrupo = form(this.modeloGrupo, (caminho) => {
    required(caminho.nome, { message: 'Informe o nome do grupo.' });
    maxLength(caminho.nome, 200, { message: 'O nome pode ter até 200 caracteres.' });
    validate(caminho.nome, ({ value }) => value().trim() ? null : { kind: 'nome', message: 'Informe o nome do grupo.' });
  }, {
    submission: {
      onInvalid: () => this.mostrarErroEditor('Revise os campos indicados antes de salvar.'),
      action: async (campos) => {
        if (!this.editorDisponivel()) return { kind: 'carregando', message: 'Aguarde os dados do cadastro.' };
        try {
          await this.api.salvarGrupo(this.modeloGrupo().nome, this.grupoAtual);
          this.editor()?.nativeElement.close();
          this.aviso.set('Grupo salvo.');
          await this.carregar(this.pagina().page);
          return;
        } catch (erro) {
          const problema = problemaCadastro(erro);
          await this.mostrarErroEditor(problema.title!);
          return { kind: 'servidor', message: problema.errors?.['nome']?.[0] ?? problema.title!, fieldTree: campos.nome };
        }
      },
    },
  });

  protected readonly formularioPlano = form(this.modeloPlano, (caminho) => {
    required(caminho.precoDiario, { message: 'Informe o preço por dia.' });
    required(caminho.precoKm, { message: 'Informe o preço por quilômetro.', when: () => this.modalidade() === 'Diario' });
    required(caminho.franquiaDiariaKm, { message: 'Informe a franquia diária.', when: () => this.modalidade() === 'Controlado' });
    required(caminho.precoKmExcedente, { message: 'Informe o preço por quilômetro excedente.', when: () => this.modalidade() === 'Controlado' });
    validate(caminho.precoDiario, ({ value }) => erroPreco(value()));
    validate(caminho.precoKm, ({ value }) => this.modalidade() === 'Diario' ? erroPreco(value()) : null);
    validate(caminho.precoKmExcedente, ({ value }) => this.modalidade() === 'Controlado' ? erroPreco(value()) : null);
    validate(caminho.franquiaDiariaKm, ({ value }) => {
      if (this.modalidade() !== 'Controlado' || !value()) return null;
      return /^\d+$/.test(value()) && BigInt(value()) <= 2147483647n ? null
        : { kind: 'franquia', message: 'Informe uma quantidade inteira não negativa de quilômetros.' };
    });
  }, {
    submission: {
      onInvalid: () => this.mostrarErroEditor('Revise os campos indicados antes de salvar.'),
      action: async (campos) => {
        if (!this.editorDisponivel() || !this.grupoDoPlano) return { kind: 'carregando', message: 'Aguarde os dados do cadastro.' };
        const modelo = this.modeloPlano();
        try {
          await this.api.salvarPlano(this.grupoDoPlano.id, this.modalidade(), {
            precoDiario: decimalCanonico(modelo.precoDiario),
            precoKm: this.modalidade() === 'Diario' ? decimalCanonico(modelo.precoKm) : null,
            franquiaDiariaKm: this.modalidade() === 'Controlado' ? Number(modelo.franquiaDiariaKm) : null,
            precoKmExcedente: this.modalidade() === 'Controlado' ? decimalCanonico(modelo.precoKmExcedente) : null,
          }, this.planoAtual);
          this.editor()?.nativeElement.close();
          this.aviso.set('Plano salvo para novas contratações.');
          await this.carregar(this.pagina().page);
          return;
        } catch (erro) {
          const problema = problemaCadastro(erro);
          await this.mostrarErroEditor(problema.title!);
          const erros = problema.errors ?? {};
          return Object.entries(erros).length ? Object.entries(erros).map(([campo, mensagens]) => ({
            kind: 'servidor', message: mensagens[0],
            fieldTree: campo === 'precoKm' ? campos.precoKm : campo === 'precoKmExcedente' ? campos.precoKmExcedente
              : campo === 'franquiaDiariaKm' ? campos.franquiaDiariaKm : campos.precoDiario,
          })) : { kind: 'servidor', message: problema.title! };
        }
      },
    },
  });
  protected readonly salvando = computed(() => this.formularioGrupo().submitting() || this.formularioPlano().submitting());

  async ngOnInit(): Promise<void> {
    await this.carregar();
  }

  protected async carregar(page = 1): Promise<void> {
    this.carregando.set(true);
    this.erroPagina.set(null);
    try {
      const pagina = await this.api.listarGrupos(page);
      this.pagina.set(pagina);
      if (!pagina.items.some((grupo) => grupo.id === this.selecionado())) this.selecionado.set(pagina.items[0]?.id ?? '');
    } catch (erro) {
      this.erroPagina.set(problemaCadastro(erro).title!);
      await focarResumo('erro-lista-grupos');
    } finally {
      this.carregando.set(false);
    }
  }

  protected mudarAba(aba: 'grupos' | 'planos', teclado = false): void {
    this.aba.set(aba);
    if (teclado) document.getElementById(`aba-${aba}`)?.focus();
  }

  protected teclaAba(evento: KeyboardEvent): void {
    if (['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(evento.key)) {
      evento.preventDefault();
      const aba = evento.key === 'Home' ? 'grupos' : evento.key === 'End' ? 'planos' : this.aba() === 'grupos' ? 'planos' : 'grupos';
      this.mudarAba(aba, true);
    }
  }

  protected selecionarGrupo(evento: Event): void {
    this.selecionado.set((evento.target as HTMLSelectElement).value);
  }

  protected planoDoGrupo(grupo: Grupo, modalidade: ModalidadePlano): Plano | undefined {
    return grupo.planos.find((plano) => plano.modalidade === modalidade);
  }

  protected async abrirGrupo(grupo?: Grupo): Promise<void> {
    const abertura = ++this.abertura;
    this.grupoAtual = null;
    this.editorTipo.set('grupo');
    this.tituloEditor.set(grupo ? 'Editar grupo' : 'Novo grupo');
    this.formularioGrupo().reset({ nome: grupo?.nome ?? '' });
    this.prepararEditor();
    if (!grupo) {
      this.editorDisponivel.set(true);
      await this.focarPrimeiroCampo();
      return;
    }
    this.carregandoEditor.set(true);
    try {
      const atual = await this.api.grupo(grupo.id);
      if (abertura !== this.abertura) return;
      this.grupoAtual = atual;
      this.formularioGrupo().reset({ nome: atual.dados.nome });
      this.editorDisponivel.set(true);
      this.carregandoEditor.set(false);
      await this.focarPrimeiroCampo();
    } catch (erro) {
      await this.mostrarErroEditor(problemaCadastro(erro).title!);
    } finally {
      if (abertura === this.abertura) this.carregandoEditor.set(false);
    }
  }

  protected async abrirPlano(grupo: Grupo, modalidade: ModalidadePlano): Promise<void> {
    const abertura = ++this.abertura;
    this.grupoDoPlano = grupo;
    this.planoAtual = null;
    this.modalidade.set(modalidade);
    this.editorTipo.set('plano');
    this.tituloEditor.set(`Plano ${this.rotulos[modalidade]} · ${grupo.nome}`);
    this.formularioPlano().reset({ precoDiario: '', precoKm: '', franquiaDiariaKm: '', precoKmExcedente: '' });
    this.prepararEditor();
    const existente = this.planoDoGrupo(grupo, modalidade);
    if (!existente) {
      this.editorDisponivel.set(true);
      await this.focarPrimeiroCampo();
      return;
    }
    this.carregandoEditor.set(true);
    try {
      const atual = await this.api.plano(existente.id);
      if (abertura !== this.abertura) return;
      this.planoAtual = atual;
      this.formularioPlano().reset({
        precoDiario: atual.dados.precoDiario.replace('.', ','),
        precoKm: atual.dados.precoKm?.replace('.', ',') ?? '',
        franquiaDiariaKm: atual.dados.franquiaDiariaKm?.toString() ?? '',
        precoKmExcedente: atual.dados.precoKmExcedente?.replace('.', ',') ?? '',
      });
      this.editorDisponivel.set(true);
      this.carregandoEditor.set(false);
      await this.focarPrimeiroCampo();
    } catch (erro) {
      await this.mostrarErroEditor(problemaCadastro(erro).title!);
    } finally {
      if (abertura === this.abertura) this.carregandoEditor.set(false);
    }
  }

  protected async excluirGrupo(grupo: Grupo): Promise<void> {
    await this.excluir(() => this.api.excluirGrupo(grupo.id), 'Grupo retirado de uso.');
  }

  protected async excluirPlano(plano: Plano): Promise<void> {
    await this.excluir(() => this.api.excluirPlano(plano.id), 'Plano retirado de uso.');
  }

  protected fecharEditor(): void {
    if (!this.salvando()) this.editor()?.nativeElement.close();
  }

  protected cancelarEditor(evento: Event): void {
    if (this.salvando()) evento.preventDefault();
  }

  protected teclaEditor(evento: KeyboardEvent): void {
    const dialogo = this.editor()?.nativeElement;
    if (dialogo) conterTabulacao(evento, dialogo);
  }

  protected editorFechado(): void {
    this.abertura++;
    if (this.origemFoco?.isConnected) this.origemFoco.focus();
    else document.getElementById('novo-grupo')?.focus();
  }

  private prepararEditor(): void {
    this.erroEditor.set(null);
    this.editorDisponivel.set(false);
    this.carregandoEditor.set(false);
    const dialogo = this.editor()?.nativeElement;
    if (dialogo && !dialogo.open) {
      this.origemFoco = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      dialogo.showModal();
    }
  }

  private async focarPrimeiroCampo(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    this.editor()?.nativeElement.querySelector<HTMLInputElement>('input')?.focus();
  }

  private async mostrarErroEditor(mensagem: string): Promise<void> {
    this.erroEditor.set(mensagem);
    await focarResumo('erro-editor-grupos');
  }

  private async excluir(acao: () => Promise<void>, mensagem: string): Promise<void> {
    this.erroPagina.set(null);
    this.aviso.set(null);
    try {
      await acao();
      this.aviso.set(mensagem);
      const pagina = this.pagina();
      await this.carregar(pagina.items.length === 1 && pagina.page > 1 ? pagina.page - 1 : pagina.page);
      document.getElementById('novo-grupo')?.focus();
    } catch (erro) {
      this.erroPagina.set(problemaCadastro(erro).title!);
      await focarResumo('erro-lista-grupos');
    }
  }
}

function erroPreco(valor: string): { kind: string; message: string } | null {
  if (!valor) return null;
  const canonico = valor.replace(',', '.');
  if (/^\d+(?:\.\d{1,6})?$/.test(canonico) && BigInt(canonico.split('.')[0]) <= 9999999999999n) return null;
  return { kind: 'preco', message: 'Informe um preço não negativo, com até seis casas decimais e treze dígitos inteiros.' };
}

function decimalCanonico(valor: string): string {
  const [inteiro, fracao] = valor.replace(',', '.').split('.');
  return BigInt(inteiro).toString() + (fracao === undefined ? '' : `.${fracao}`);
}
