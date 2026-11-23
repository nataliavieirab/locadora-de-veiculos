import { DatePipe } from '@angular/common';
import { Component, computed, ElementRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { form, FormField, FormRoot, maxLength, required, validate } from '@angular/forms/signals';
import { ShellCadastralComponent } from '../../shared/shell-cadastral.component';
import { CampoTextoComponent } from '../../shared/campo-texto.component';
import { problemaCadastro } from '../../shared/erro-cadastro';
import { conterTabulacao, focarResumo } from '../../shared/foco';
import { MoedaBrlPipe } from '../../shared/moeda-brl.pipe';
import { ResumoErroComponent } from '../../shared/resumo-erro.component';
import { Pagina, Recurso } from '../frota/grupos-planos.service';
import { Combustivel as TipoCombustivel } from '../frota/veiculos.service';
import { DadosTaxa, PrecoCombustivel, PrecosService, SeguroVersionado, TaxaServico, TipoSeguro } from './precos.service';

type Aba = 'taxas' | 'seguros' | 'combustiveis';
const taxaVazia = (): DadosTaxa => ({ nome: '', preco: '', modalidade: 'Fixa' });

@Component({
  selector: 'app-precos',
  imports: [DatePipe, FormRoot, FormField, ShellCadastralComponent, CampoTextoComponent, MoedaBrlPipe, ResumoErroComponent],
  templateUrl: './precos.component.html',
})
export class PrecosComponent implements OnInit {
  private readonly api = inject(PrecosService);
  private readonly editor = viewChild<ElementRef<HTMLDialogElement>>('editor');
  private taxaAtual: Recurso<TaxaServico> | null = null;
  private origemFoco: HTMLElement | null = null;
  private abertura = 0;
  protected readonly aba = signal<Aba>('taxas');
  protected readonly abas: Aba[] = ['taxas', 'seguros', 'combustiveis'];
  protected readonly pagina = signal<Pagina<TaxaServico>>({ items: [], page: 1, pageSize: 20, total: 0 });
  protected readonly seguros = signal<Partial<Record<TipoSeguro, SeguroVersionado>>>({});
  protected readonly combustiveis = signal<PrecoCombustivel[]>([]);
  protected readonly carregando = signal(true);
  protected readonly erroPagina = signal<string | null>(null);
  protected readonly erroAcao = signal<string | null>(null);
  protected readonly erroEditor = signal<string | null>(null);
  protected readonly erroSeguros = signal<string | null>(null);
  protected readonly aviso = signal<string | null>(null);
  protected readonly editorTipo = signal<'taxa' | 'combustivel'>('taxa');
  protected readonly editorDisponivel = signal(false);
  protected readonly carregandoEditor = signal(false);
  protected readonly combustivelEditado = signal<TipoCombustivel>('Gasolina');
  protected readonly tituloEditor = signal('Nova taxa ou serviço');
  protected readonly tiposSeguro: TipoSeguro[] = ['Cliente', 'Terceiros'];
  protected readonly tiposCombustivel: TipoCombustivel[] = ['Gasolina', 'Gas', 'Diesel', 'Alcool'];
  protected readonly rotulosSeguro = { Cliente: 'Seguro do cliente', Terceiros: 'Seguro de terceiros' };
  protected readonly rotulosCombustivel = { Gasolina: 'Gasolina', Gas: 'Gás', Diesel: 'Diesel', Alcool: 'Álcool' };
  private readonly modeloTaxa = signal(taxaVazia());
  private readonly modeloCombustivel = signal({ preco: '' });
  private readonly modeloSeguros = signal({ Cliente: '', Terceiros: '' });

  protected readonly formularioTaxa = form(this.modeloTaxa, (caminho) => {
    required(caminho.nome, { message: 'Informe o nome.' });
    maxLength(caminho.nome, 200, { message: 'O nome pode ter até 200 caracteres.' });
    validate(caminho.nome, ({ value }) => value().trim() ? null : { kind: 'nome', message: 'Informe o nome.' });
    required(caminho.preco, { message: 'Informe o preço.' });
    validate(caminho.preco, ({ value }) => erroPreco(value()));
    required(caminho.modalidade, { message: 'Selecione a modalidade.' });
  }, {
    submission: {
      onInvalid: () => this.mostrarErroEditor('Revise os campos indicados antes de salvar.'),
      action: async (campos) => {
        if (!this.editorDisponivel()) return { kind: 'carregando', message: 'Aguarde o cadastro.' };
        try {
          await this.api.salvarTaxa({ ...this.modeloTaxa(), preco: canonico(this.modeloTaxa().preco) }, this.taxaAtual);
          this.editor()?.nativeElement.close();
          this.aviso.set('Taxa ou serviço salvo para novas operações.');
          await this.carregarTaxas(this.pagina().page);
          return;
        } catch (erro) {
          const problema = problemaCadastro(erro);
          await this.mostrarErroEditor(problema.code === 'taxa_com_aluguel_aberto'
            ? 'Esta taxa está vinculada a um aluguel aberto. Os dados foram mantidos.' : problema.title!);
          return Object.entries(problema.errors ?? {}).map(([campo, mensagens]) => ({
            kind: 'servidor', message: mensagens[0],
            fieldTree: campo === 'preco' ? campos.preco : campo === 'modalidade' ? campos.modalidade : campos.nome,
          }));
        }
      },
    },
  });

  protected readonly formularioCombustivel = form(this.modeloCombustivel, (caminho) => {
    required(caminho.preco, { message: 'Informe o preço.' });
    validate(caminho.preco, ({ value }) => erroPreco(value()));
  }, {
    submission: {
      onInvalid: () => this.mostrarErroEditor('Revise o preço antes de criar a vigência.'),
      action: async (campos) => {
        try {
          await this.api.registrarCombustivel(this.combustivelEditado(), canonico(this.modeloCombustivel().preco));
          this.editor()?.nativeElement.close();
          this.aviso.set('Nova vigência criada no horário do servidor. As anteriores foram preservadas.');
          await this.carregarCombustiveis();
          return;
        } catch (erro) {
          const problema = problemaCadastro(erro);
          await this.mostrarErroEditor(problema.title!);
          return { kind: 'servidor', message: problema.errors?.['precoUnitario']?.[0] ?? problema.title!, fieldTree: campos.preco };
        }
      },
    },
  });

  protected readonly formularioSeguros = form(this.modeloSeguros, (caminho) => {
    for (const tipo of this.tiposSeguro) {
      required(caminho[tipo], { message: 'Informe o preço diário.', when: () => this.seguros()[tipo]?.dados.configurado ?? false });
      validate(caminho[tipo], ({ value }) => erroPreco(value()));
    }
  }, {
    submission: {
      onInvalid: () => this.mostrarErroSeguros('Revise os preços indicados antes de salvar.'),
      action: async (campos) => {
        this.erroSeguros.set(null);
        this.aviso.set(null);
        let gravados = 0;
        for (const tipo of this.tiposSeguro) {
          const valor = this.modeloSeguros()[tipo];
          const atual = this.seguros()[tipo];
          if (!valor || !atual) continue;
          const preco = canonico(valor);
          if (atual.dados.precoDiario !== null && canonico(atual.dados.precoDiario) === preco) continue;
          try {
            const salvo = await this.api.salvarSeguro(tipo, preco, atual);
            this.seguros.update((itens) => ({ ...itens, [tipo]: salvo }));
            gravados++;
          } catch (erro) {
            const problema = problemaCadastro(erro);
            await this.mostrarErroSeguros(`${this.rotulosSeguro[tipo]}: ${problema.title}${gravados ? ' O outro preço já foi salvo; as configurações são independentes.' : ''}`);
            return { kind: 'servidor', message: problema.errors?.['precoDiario']?.[0] ?? problema.title!, fieldTree: campos[tipo] };
          }
        }
        this.aviso.set(gravados ? 'Preços dos seguros salvos de forma independente para novas contratações.' : 'Nenhum preço foi alterado. Para configurar, informe um valor; zero é permitido.');
        return;
      },
    },
  });
  protected readonly salvando = computed(() =>
    this.formularioTaxa().submitting() || this.formularioCombustivel().submitting());

  async ngOnInit(): Promise<void> {
    await this.carregar();
  }

  protected async carregar(): Promise<void> {
    this.carregando.set(true);
    this.erroPagina.set(null);
    try {
      const [pagina, cliente, terceiros, combustiveis] = await Promise.all([
        this.api.listarTaxas(), this.api.seguro('Cliente'), this.api.seguro('Terceiros'),
        Promise.all(this.tiposCombustivel.map((tipo) => this.api.combustivel(tipo))),
      ]);
      this.pagina.set(pagina);
      this.seguros.set({ Cliente: cliente, Terceiros: terceiros });
      this.formularioSeguros().reset({
        Cliente: cliente.dados.precoDiario?.replace('.', ',') ?? '',
        Terceiros: terceiros.dados.precoDiario?.replace('.', ',') ?? '',
      });
      this.combustiveis.set(combustiveis);
    } catch (erro) {
      this.erroPagina.set(problemaCadastro(erro).title!);
      await focarResumo('erro-pagina-precos');
    } finally {
      this.carregando.set(false);
    }
  }

  protected mudarAba(aba: Aba, teclado = false): void {
    this.aba.set(aba);
    this.erroAcao.set(null);
    if (teclado) document.getElementById(`aba-${aba}`)?.focus();
  }

  protected teclaAba(evento: KeyboardEvent): void {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(evento.key)) return;
    evento.preventDefault();
    const abas: Aba[] = ['taxas', 'seguros', 'combustiveis'];
    const indice = abas.indexOf(this.aba());
    this.mudarAba(evento.key === 'Home' ? abas[0] : evento.key === 'End' ? abas[2]
      : abas[(indice + (evento.key === 'ArrowRight' ? 1 : -1) + 3) % 3], true);
  }

  protected async carregarTaxas(page: number): Promise<void> {
    try {
      this.pagina.set(await this.api.listarTaxas(page));
    } catch (erro) {
      this.erroAcao.set(problemaCadastro(erro).title!);
      await focarResumo('erro-acao-precos');
    }
  }

  protected async abrirTaxa(taxa?: TaxaServico): Promise<void> {
    const abertura = ++this.abertura;
    this.editorTipo.set('taxa');
    this.tituloEditor.set(taxa ? 'Editar taxa ou serviço' : 'Nova taxa ou serviço');
    this.taxaAtual = null;
    this.formularioTaxa().reset(taxaVazia());
    this.prepararEditor();
    if (taxa) {
      this.carregandoEditor.set(true);
      try {
        const atual = await this.api.taxa(taxa.id);
        if (abertura !== this.abertura) return;
        this.taxaAtual = atual;
        this.formularioTaxa().reset({ nome: atual.dados.nome, preco: atual.dados.preco.replace('.', ','), modalidade: atual.dados.modalidade });
      } catch (erro) {
        if (abertura === this.abertura) await this.mostrarErroEditor(problemaCadastro(erro).title!);
        return;
      } finally {
        if (abertura === this.abertura) this.carregandoEditor.set(false);
      }
    }
    this.editorDisponivel.set(true);
    await this.focarCampo('nome-taxa');
  }

  protected async abrirCombustivel(tipo: TipoCombustivel): Promise<void> {
    this.abertura++;
    this.editorTipo.set('combustivel');
    this.combustivelEditado.set(tipo);
    this.tituloEditor.set(`Novo preço · ${this.rotulosCombustivel[tipo]}`);
    this.formularioCombustivel().reset({ preco: '' });
    this.prepararEditor();
    this.editorDisponivel.set(true);
    await this.focarCampo('preco-combustivel');
  }

  protected async excluir(taxa: TaxaServico): Promise<void> {
    this.erroAcao.set(null);
    try {
      await this.api.excluirTaxa(taxa.id);
      const pagina = this.pagina();
      await this.carregarTaxas(pagina.items.length === 1 && pagina.page > 1 ? pagina.page - 1 : pagina.page);
      this.aviso.set('Taxa retirada de uso. Valores históricos foram preservados.');
      document.getElementById('nova-taxa')?.focus();
    } catch (erro) {
      const problema = problemaCadastro(erro);
      this.erroAcao.set(problema.code === 'taxa_com_aluguel_aberto'
        ? 'Esta taxa está vinculada a um aluguel aberto e foi mantida sem alterações.' : problema.title!);
      await focarResumo('erro-acao-precos');
    }
  }

  protected fecharEditor(): void {
    if (!this.salvando()) this.editor()?.nativeElement.close();
  }

  protected cancelarEditor(evento: Event): void {
    if (this.salvando()) evento.preventDefault();
  }

  protected teclaEditor(evento: KeyboardEvent): void {
    const dialogo = this.editor()?.nativeElement;
    if (dialogo?.open) conterTabulacao(evento, dialogo);
  }

  protected editorFechado(): void {
    this.abertura++;
    if (this.origemFoco?.isConnected) this.origemFoco.focus();
    if (this.editor()?.nativeElement.contains(document.activeElement)) document.getElementById('conteudo-precos')?.focus();
  }

  private prepararEditor(): void {
    this.erroEditor.set(null);
    this.editorDisponivel.set(false);
    this.carregandoEditor.set(false);
    this.origemFoco = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.editor()?.nativeElement.showModal();
  }

  private async carregarCombustiveis(): Promise<void> {
    try {
      this.combustiveis.set(await Promise.all(this.tiposCombustivel.map((tipo) => this.api.combustivel(tipo))));
    } catch (erro) {
      this.erroAcao.set(problemaCadastro(erro).title!);
      await focarResumo('erro-acao-precos');
    }
  }

  private async focarCampo(id: string): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    document.getElementById(id)?.focus();
  }

  private async mostrarErroEditor(mensagem: string): Promise<void> {
    this.erroEditor.set(mensagem);
    await focarResumo('erro-editor-precos');
  }

  private async mostrarErroSeguros(mensagem: string): Promise<void> {
    this.erroSeguros.set(mensagem);
    await focarResumo('erro-seguros');
  }
}

function erroPreco(valor: string): { kind: string; message: string } | null {
  if (!valor) return null;
  const texto = valor.replace(',', '.');
  return /^\d+(?:\.\d{1,6})?$/.test(texto) && BigInt(texto.split('.')[0]) <= 9999999999999n ? null
    : { kind: 'preco', message: 'Informe um preço não negativo, com até seis casas decimais e treze dígitos inteiros.' };
}

function canonico(valor: string): string {
  const [inteiro, fracao] = valor.replace(',', '.').split('.');
  const parte = fracao?.replace(/0+$/, '');
  return BigInt(inteiro).toString() + (parte ? `.${parte}` : '');
}
