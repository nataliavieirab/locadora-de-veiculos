import { HttpClient, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { URL_API } from '../../core/configuracao';
import { Pagina, Recurso } from '../frota/grupos-planos.service';

export interface ItemListaAluguel {
  id: string;
  estado: string;
  revisao: number;
  condutorNome: string;
  condutorCpfResumo: string;
  veiculoPlaca: string;
  planoModalidade: string;
  saidaUtc: string;
  retornoPrevistoUtc: string;
  totalConhecido: string;
}

export interface ComponenteEstimativa {
  categoria: string | null;
  origemId: string;
  tipoSeguro: string | null;
  nome: string;
  precoUnitario: string;
  multiplicador: number;
  valorBruto: string;
  valorArredondado: string;
}

export interface EstimativaContrato {
  versaoRegra: string;
  calculadoEmUtc: string;
  saidaUtc: string;
  retornoPrevistoUtc: string;
  diarias: number;
  quilometros: number | null;
  franquiaTotalKm: number | null;
  quilometragemPendente: boolean;
  combustivelPendente: boolean;
  atrasoPendente: boolean;
  termoDiariasBruto: string;
  termoKmBruto: string;
  componentes: ComponenteEstimativa[];
  totalConhecido: string;
  caucao: string;
}

export interface ContratoAluguel {
  id: string;
  estado: string;
  revisao: number;
  clienteId: string;
  condutorId: string;
  veiculoId: string;
  planoId: string;
  saidaUtc: string;
  retornoPrevistoUtc: string;
  hodometroInicial: number;
  caucao: string;
  cliente: {
    tipo: string;
    nome: string;
    cpf: string | null;
    cnpj: string | null;
    rg: string | null;
    cnh: string | null;
    telefone: string;
    email: string | null;
    endereco: { estado: string; cidade: string; bairro: string; rua: string; numero: string };
  };
  condutor: { nome: string; cpf: string; cnh: string; validadeCnh: string; email: string; telefone: string };
  veiculo: {
    placa: string; marca: string; cor: string; modelo: string; combustivel: string;
    capacidadeLitros: string; ano: number; grupoId: string; grupoNome: string;
  };
  plano: {
    modalidade: string; precoDiario: string; precoKm: string | null;
    franquiaDiariaKm: number | null; precoKmExcedente: string | null;
  };
  itens: Array<{
    categoria: string; origemId: string; tipoSeguro: string | null; nome: string; modalidade: string;
    precoUnitario: string; origemInclusao: string; revisaoPrimeiraInclusao: number;
  }>;
  estimativa: EstimativaContrato;
}

export interface FiltroAlugueis {
  estado?: string;
  veiculoId?: string;
  saidaDe?: string;
  saidaAte?: string;
}

export interface EntradaContratacao {
  clienteId: string;
  condutorId: string;
  veiculoId: string;
  planoId: string;
  retornoPrevisto: string;
  hodometroInicial: number;
  taxasServicoIds: string[];
  seguros: ('Cliente' | 'Terceiros')[];
}

export interface EntradaEstimativa extends EntradaContratacao {
  aluguelId?: string;
}

@Injectable({ providedIn: 'root' })
export class AlugueisService {
  private readonly http = inject(HttpClient);
  private readonly base = `${URL_API}/api/v1/alugueis`;

  listar(page = 1, pageSize = 20, filtro: FiltroAlugueis = {}): Promise<Pagina<ItemListaAluguel>> {
    const params: Record<string, string | number> = { page, pageSize };
    if (filtro.estado) params['estado'] = filtro.estado;
    if (filtro.veiculoId) params['veiculoId'] = filtro.veiculoId;
    if (filtro.saidaDe) params['saidaDe'] = filtro.saidaDe;
    if (filtro.saidaAte) params['saidaAte'] = filtro.saidaAte;
    return firstValueFrom(this.http.get<Pagina<ItemListaAluguel>>(this.base, { params }));
  }

  async consultar(id: string): Promise<Recurso<ContratoAluguel>> {
    const resposta = await firstValueFrom(this.http.get<ContratoAluguel>(`${this.base}/${id}`, { observe: 'response' }));
    const etag = resposta.headers.get('ETag');
    if (!resposta.body || !etag) throw new Error('A API não retornou os dados e a versão esperados.');
    return { dados: resposta.body, etag };
  }

  estimar(entrada: EntradaEstimativa, ifMatch?: string): Promise<ContratoAluguel> {
    const headers = ifMatch ? { 'If-Match': ifMatch } : undefined;
    return firstValueFrom(this.http.post<ContratoAluguel>(`${this.base}/estimativas`, entrada, { headers }));
  }

  async criar(entrada: EntradaContratacao, chaveIdempotencia: string): Promise<Recurso<ContratoAluguel>> {
    const resposta = await firstValueFrom(this.http.post<ContratoAluguel>(this.base, entrada, {
      observe: 'response',
      headers: { 'Idempotency-Key': chaveIdempotencia },
    }));
    const etag = resposta.headers.get('ETag');
    if (!resposta.body || !etag) throw new Error('A API não confirmou a contratação com a versão esperada.');
    return { dados: resposta.body, etag };
  }

  async alterar(id: string, entrada: EntradaContratacao, ifMatch: string): Promise<Recurso<ContratoAluguel>> {
    const resposta = await firstValueFrom(this.http.put<ContratoAluguel>(`${this.base}/${id}`, entrada, {
      observe: 'response',
      headers: { 'If-Match': ifMatch },
    }));
    const etag = resposta.headers.get('ETag');
    if (!resposta.body || !etag) throw new Error('A API não confirmou a edição com a versão esperada.');
    return { dados: resposta.body, etag };
  }
}