import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { URL_API } from '../../core/configuracao';
import { Grupo, GruposPlanosService, Pagina, Recurso } from './grupos-planos.service';

export type Combustivel = 'Gasolina' | 'Gas' | 'Diesel' | 'Alcool';
export interface DadosVeiculo {
  placa: string;
  marca: string;
  cor: string;
  modelo: string;
  combustivel: Combustivel;
  capacidadeLitros: string;
  ano: number;
  grupoId: string;
}
export interface Veiculo extends DadosVeiculo { id: string }

@Injectable({ providedIn: 'root' })
export class VeiculosService {
  private readonly http = inject(HttpClient);
  private readonly grupos = inject(GruposPlanosService);
  private readonly base = `${URL_API}/api/v1/veiculos`;

  listar(page = 1, grupoId = ''): Promise<Pagina<Veiculo>> {
    return firstValueFrom(this.http.get<Pagina<Veiculo>>(this.base, {
      params: { page, pageSize: 20, ...(grupoId ? { grupoId } : {}) },
    }));
  }

  async listarGrupos(): Promise<Grupo[]> {
    const grupos: Grupo[] = [];
    let pagina = 1;
    for (;;) {
      const resposta = await this.grupos.listarGrupos(pagina++);
      grupos.push(...resposta.items);
      if (!resposta.items.length || resposta.page * resposta.pageSize >= resposta.total) return grupos;
    }
  }

  async consultar(id: string): Promise<Recurso<Veiculo>> {
    const resposta = await firstValueFrom(this.http.get<Veiculo>(`${this.base}/${id}`, { observe: 'response' }));
    const etag = resposta.headers.get('ETag');
    if (!resposta.body || !etag) throw new Error('A API não retornou os dados e a versão esperados.');
    return { dados: resposta.body, etag };
  }

  async salvar(dados: DadosVeiculo, atual: Recurso<Veiculo> | null): Promise<void> {
    if (atual) {
      await firstValueFrom(this.http.put(`${this.base}/${atual.dados.id}`, dados, { headers: { 'If-Match': atual.etag } }));
    } else {
      await firstValueFrom(this.http.post(this.base, dados));
    }
  }

  async excluir(id: string): Promise<void> {
    const atual = await this.consultar(id);
    await firstValueFrom(this.http.delete(`${this.base}/${id}`, { headers: { 'If-Match': atual.etag } }));
  }
}
