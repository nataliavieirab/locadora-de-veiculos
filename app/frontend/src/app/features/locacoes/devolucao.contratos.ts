export interface EntradaDevolucao {
  retornoReal: string;
  hodometroFinal: number;
  litrosRestantes: string;
  taxasAdicionaisIds: string[];
}

export interface VersoesDevolucaoAceitas {
  revisaoId: string;
  versaoRegra: string;
  combustivelId: string;
  taxasNovas: Array<{ id: string; versao: string }>;
  resumoCalculo: string;
}

export interface EntradaConclusaoDevolucao extends EntradaDevolucao {
  versoesAceitas: VersoesDevolucaoAceitas;
}

export interface ApuracaoDevolucao {
  versaoRegra: string;
  saidaUtc: string;
  retornoPrevistoUtc: string;
  retornoRealUtc: string;
  hodometroInicial: number;
  hodometroFinal: number;
  litrosRestantes: string;
  capacidadeLitros: string;
  litrosRepostos: string;
  diarias: number;
  quilometros: number;
  franquiaTotalKm: number | null;
  excedenteKm: number | null;
  emAtraso: boolean;
  termoDiariasBruto: string;
  termoKmBruto: string;
  combustivel: { id: string; tipo: string; inicioVigenciaUtc: string; precoUnitario: string };
  taxasNovas: Array<{ id: string; versao: string; nome: string; modalidade: string; precoUnitario: string; origemInclusao: string }>;
  componentes: Array<{
    categoria: string; origemId: string | null; tipoSeguro: string | null; nome: string;
    modalidade: string | null; origemInclusao: string | null; quantidade: string;
    precoUnitario: string; valorBruto: string; valorArredondado: string;
  }>;
  totalFinal: string;
  caucao: string;
}

export interface PreviaDevolucao {
  aluguelId: string;
  revisaoId: string;
  revisao: number;
  versaoAluguel: string;
  calculo: ApuracaoDevolucao;
  versoesAceitas: VersoesDevolucaoAceitas;
}

export interface FinalDevolucao {
  id: string;
  revisaoId: string;
  calculadoEmUtc: string;
  registradoEmUtc: string;
  calculo: ApuracaoDevolucao;
}

export interface ResultadoDevolucao {
  aluguelId: string;
  estado: 'Concluido';
  devolucao: FinalDevolucao;
}
