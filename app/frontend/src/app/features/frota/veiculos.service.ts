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
export interface FotoVeiculo {
  id: string; caminho: string; mime: 'image/jpeg' | 'image/png'; tamanhoBytes: number; largura: number; altura: number;
}
export interface Veiculo extends DadosVeiculo { id: string; foto?: FotoVeiculo | null; possuiAluguelAberto?: boolean }

@Injectable({ providedIn: 'root' })
export class VeiculosService {
  private readonly http = inject(HttpClient);
  private readonly grupos = inject(GruposPlanosService);
  private readonly base = `${URL_API}/api/v1/veiculos`;

  listar(page = 1, grupoId = '', somenteDisponiveis = false, aluguelEmEdicaoId?: string): Promise<Pagina<Veiculo>> {
    return firstValueFrom(this.http.get<Pagina<Veiculo>>(this.base, {
      params: {
        page, pageSize: 20,
        ...(grupoId ? { grupoId } : {}),
        ...(somenteDisponiveis ? { somenteDisponiveis: true } : {}),
        ...(aluguelEmEdicaoId ? { aluguelEmEdicaoId } : {}),
      },
    }));
  }

  async listarDisponiveis(aluguelEmEdicaoId?: string): Promise<Veiculo[]> {
    const veiculos: Veiculo[] = [];
    let pagina = 1;
    for (;;) {
      const resposta = aluguelEmEdicaoId
        ? await this.listar(pagina++, '', false, aluguelEmEdicaoId)
        : await this.listar(pagina++, '', true);
      veiculos.push(...resposta.items);
      if (!resposta.items.length || resposta.page * resposta.pageSize >= resposta.total) return veiculos;
    }
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

  async salvar(dados: DadosVeiculo, atual: Recurso<Veiculo> | null): Promise<Recurso<Veiculo>> {
    const resposta = await firstValueFrom(atual
      ? this.http.put<Veiculo>(`${this.base}/${atual.dados.id}`, dados, { headers: { 'If-Match': atual.etag }, observe: 'response' })
      : this.http.post<Veiculo>(this.base, dados, { observe: 'response' }));
    const etag = resposta.headers.get('ETag');
    if (!resposta.body || !etag) throw new Error('A API não retornou os dados e a versão esperados.');
    return { dados: { ...resposta.body, foto: atual?.dados.foto ?? null, possuiAluguelAberto: atual?.dados.possuiAluguelAberto ?? false }, etag };
  }

  lerFoto(id: string): Promise<Blob> {
    // A URL é construída localmente. Não seguir caminhos arbitrários recebidos em DTO.
    return firstValueFrom(this.http.get(`${this.base}/${id}/foto`, { responseType: 'blob' }));
  }

  async enviarFoto(id: string, etag: string, arquivo: File): Promise<Recurso<FotoVeiculo>> {
    const corpo = new FormData();
    corpo.append('arquivo', arquivo, 'imagem');
    const resposta = await firstValueFrom(this.http.put<{ foto: FotoVeiculo }>(`${this.base}/${id}/foto`, corpo,
      { headers: { 'If-Match': etag }, observe: 'response' }));
    const versao = resposta.headers.get('ETag');
    if (!resposta.body?.foto || !versao) throw new Error('Resultado da foto não confirmado.');
    return { dados: resposta.body.foto, etag: versao };
  }

  async removerFoto(id: string, etag: string): Promise<string> {
    const resposta = await firstValueFrom(this.http.delete(`${this.base}/${id}/foto`,
      { headers: { 'If-Match': etag }, observe: 'response' }));
    const versao = resposta.headers.get('ETag');
    if (!versao) throw new Error('Resultado da remoção não confirmado.');
    return versao;
  }

  async excluir(id: string): Promise<void> {
    const atual = await this.consultar(id);
    await firstValueFrom(this.http.delete(`${this.base}/${id}`, { headers: { 'If-Match': atual.etag } }));
  }
}
