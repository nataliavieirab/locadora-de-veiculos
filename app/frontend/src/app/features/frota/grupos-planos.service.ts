import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { URL_API } from '../../core/configuracao';

export type ModalidadePlano = 'Diario' | 'Controlado' | 'Livre';
export interface Plano {
  id: string;
  grupoId: string;
  modalidade: ModalidadePlano;
  precoDiario: string;
  precoKm: string | null;
  franquiaDiariaKm: number | null;
  precoKmExcedente: string | null;
}
export interface Grupo { id: string; nome: string; planos: Plano[] }
export interface Pagina<T> { items: T[]; page: number; pageSize: number; total: number }
export interface Recurso<T> { dados: T; etag: string }
export interface PrecosPlano {
  precoDiario: string;
  precoKm: string | null;
  franquiaDiariaKm: number | null;
  precoKmExcedente: string | null;
}

@Injectable({ providedIn: 'root' })
export class GruposPlanosService {
  private readonly http = inject(HttpClient);
  private readonly base = `${URL_API}/api/v1`;

  listarGrupos(page = 1): Promise<Pagina<Grupo>> {
    return firstValueFrom(this.http.get<Pagina<Grupo>>(`${this.base}/grupos`, { params: { page, pageSize: 20 } }));
  }

  async grupo(id: string): Promise<Recurso<Grupo>> {
    return recurso(await firstValueFrom(this.http.get<Grupo>(`${this.base}/grupos/${id}`, { observe: 'response' })));
  }

  async salvarGrupo(nome: string, atual: Recurso<Grupo> | null): Promise<void> {
    if (atual) {
      await firstValueFrom(this.http.put(`${this.base}/grupos/${atual.dados.id}`, { nome }, { headers: { 'If-Match': atual.etag } }));
    } else {
      await firstValueFrom(this.http.post(`${this.base}/grupos`, { nome }));
    }
  }

  async excluirGrupo(id: string): Promise<void> {
    const atual = await this.grupo(id);
    await firstValueFrom(this.http.delete(`${this.base}/grupos/${id}`, { headers: { 'If-Match': atual.etag } }));
  }

  async plano(id: string): Promise<Recurso<Plano>> {
    return recurso(await firstValueFrom(this.http.get<Plano>(`${this.base}/planos/${id}`, { observe: 'response' })));
  }

  async salvarPlano(grupoId: string, modalidade: ModalidadePlano, precos: PrecosPlano, atual: Recurso<Plano> | null): Promise<void> {
    if (atual) {
      await firstValueFrom(this.http.put(`${this.base}/planos/${atual.dados.id}`, precos, { headers: { 'If-Match': atual.etag } }));
    } else {
      await firstValueFrom(this.http.post(`${this.base}/planos`, { grupoId, modalidade, ...precos }));
    }
  }

  async excluirPlano(id: string): Promise<void> {
    const atual = await this.plano(id);
    await firstValueFrom(this.http.delete(`${this.base}/planos/${id}`, { headers: { 'If-Match': atual.etag } }));
  }
}

function recurso<T>(resposta: HttpResponse<T>): Recurso<T> {
  const etag = resposta.headers.get('ETag');
  if (!resposta.body || !etag) throw new Error('A API não retornou os dados e a versão esperados.');
  return { dados: resposta.body, etag };
}
