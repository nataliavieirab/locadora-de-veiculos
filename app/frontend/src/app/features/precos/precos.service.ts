import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { URL_API } from '../../core/configuracao';
import { Pagina, Recurso } from '../frota/grupos-planos.service';
import { Combustivel as TipoCombustivel } from '../frota/veiculos.service';

export type TipoSeguro = 'Cliente' | 'Terceiros';
export type ModalidadeTaxa = 'Fixa' | 'Diaria';
export interface DadosTaxa { nome: string; preco: string; modalidade: ModalidadeTaxa }
export interface TaxaServico extends DadosTaxa { id: string }
export interface PrecoSeguro { id: string | null; tipo: TipoSeguro; configurado: boolean; precoDiario: string | null }
export interface SeguroVersionado { dados: PrecoSeguro; etag: string | null }
export interface Vigencia { id: string; precoUnitario: string; inicioVigenciaUtc: string }
export interface PrecoCombustivel { tipo: TipoCombustivel; configurado: boolean; atual: Vigencia | null; historico: Vigencia[] }

@Injectable({ providedIn: 'root' })
export class PrecosService {
  private readonly http = inject(HttpClient);
  private readonly base = `${URL_API}/api/v1`;

  listarTaxas(page = 1): Promise<Pagina<TaxaServico>> {
    return firstValueFrom(this.http.get<Pagina<TaxaServico>>(`${this.base}/taxas-servicos`, { params: { page, pageSize: 20 } }));
  }

  async listarTodasTaxas(): Promise<TaxaServico[]> {
    const taxas: TaxaServico[] = [];
    let pagina = 1;
    for (;;) {
      const resposta = await this.listarTaxas(pagina++);
      taxas.push(...resposta.items);
      if (!resposta.items.length || resposta.page * resposta.pageSize >= resposta.total) return taxas;
    }
  }

  async taxa(id: string): Promise<Recurso<TaxaServico>> {
    const resposta = await firstValueFrom(this.http.get<TaxaServico>(`${this.base}/taxas-servicos/${id}`, { observe: 'response' }));
    const etag = resposta.headers.get('ETag');
    if (!resposta.body || !etag) throw new Error('A API não retornou o cadastro e a versão esperados.');
    return { dados: resposta.body, etag };
  }

  async salvarTaxa(dados: DadosTaxa, atual: Recurso<TaxaServico> | null): Promise<void> {
    if (atual) {
      await firstValueFrom(this.http.put(`${this.base}/taxas-servicos/${atual.dados.id}`, dados, { headers: { 'If-Match': atual.etag } }));
    } else {
      await firstValueFrom(this.http.post(`${this.base}/taxas-servicos`, dados));
    }
  }

  async excluirTaxa(id: string): Promise<void> {
    const atual = await this.taxa(id);
    await firstValueFrom(this.http.delete(`${this.base}/taxas-servicos/${id}`, { headers: { 'If-Match': atual.etag } }));
  }

  async seguro(tipo: TipoSeguro): Promise<SeguroVersionado> {
    const resposta = await firstValueFrom(this.http.get<PrecoSeguro>(`${this.base}/configuracoes/seguros/${tipo}`, { observe: 'response' }));
    const etag = resposta.headers.get('ETag');
    if (!resposta.body || (resposta.body.configurado && !etag)) throw new Error('A API não retornou a configuração e a versão esperadas.');
    return { dados: resposta.body, etag };
  }

  async salvarSeguro(tipo: TipoSeguro, precoDiario: string, atual: SeguroVersionado): Promise<SeguroVersionado> {
    const resposta = await firstValueFrom(this.http.put<PrecoSeguro>(`${this.base}/configuracoes/seguros/${tipo}`,
      { precoDiario }, { observe: 'response', headers: atual.etag ? { 'If-Match': atual.etag } : {} }));
    const etag = resposta.headers.get('ETag');
    if (!resposta.body || !etag) throw new Error('A API não retornou a configuração e a versão esperadas.');
    return { dados: resposta.body, etag };
  }

  combustivel(tipo: TipoCombustivel): Promise<PrecoCombustivel> {
    return firstValueFrom(this.http.get<PrecoCombustivel>(`${this.base}/configuracoes/combustiveis/${tipo}`));
  }

  async registrarCombustivel(tipo: TipoCombustivel, precoUnitario: string): Promise<void> {
    await firstValueFrom(this.http.put(`${this.base}/configuracoes/combustiveis/${tipo}`, { precoUnitario }));
  }
}
