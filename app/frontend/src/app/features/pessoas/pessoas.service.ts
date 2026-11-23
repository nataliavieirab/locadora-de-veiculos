import { HttpClient, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { URL_API } from '../../core/configuracao';
import { Pagina, Recurso } from '../frota/grupos-planos.service';

export type TipoCliente = 'PF' | 'PJ';

export interface Endereco {
  estado: string;
  cidade: string;
  bairro: string;
  rua: string;
  numero: string;
}

export interface ResumoCliente {
  id: string;
  tipo: TipoCliente;
  nome: string;
  telefone: string;
  email: string | null;
  cpf: string | null;
  cnpj: string | null;
  rg: string | null;
  cnh: string | null;
}

export interface ClienteDetalhe {
  id: string;
  tipo: TipoCliente;
  nome: string;
  telefone: string;
  email: string | null;
  endereco: Endereco;
  cpf?: string;
  cnpj?: string;
  rg?: string;
  cnh?: string;
}

export interface DadosClienteComum {
  nome: string;
  telefone: string;
  email: string | null;
  endereco: Endereco;
}

export type DadosCliente =
  | (DadosClienteComum & { tipo: 'PF'; cpf: string; rg: string; cnh: string })
  | (DadosClienteComum & { tipo: 'PJ'; cnpj: string });

export interface Condutor {
  id: string;
  clienteId: string;
  clienteNome: string;
  nome: string;
  email: string;
  cpf: string;
  cnh: string;
  validadeCnh: string;
  telefone: string;
}

export interface DadosCondutor {
  clienteId: string;
  nome: string;
  email: string;
  cpf: string;
  cnh: string;
  validadeCnh: string;
  telefone: string;
  proprioCliente: boolean;
}

export interface Vinculo {
  id: string;
  clientePfId: string;
  clientePfNome: string;
  clientePjId: string;
  clientePjNome: string;
}

@Injectable({ providedIn: 'root' })
export class PessoasService {
  private readonly http = inject(HttpClient);
  private readonly base = `${URL_API}/api/v1`;

  listarClientes(page = 1): Promise<Pagina<ResumoCliente>> {
    return firstValueFrom(this.http.get<Pagina<ResumoCliente>>(`${this.base}/clientes`, {
      params: { page, pageSize: 20 },
    }));
  }

  listarCondutores(page = 1): Promise<Pagina<Condutor>> {
    return firstValueFrom(this.http.get<Pagina<Condutor>>(`${this.base}/condutores`, {
      params: { page, pageSize: 20 },
    }));
  }

  async listarTodosClientes(): Promise<ResumoCliente[]> {
    const clientes: ResumoCliente[] = [];
    let pagina = 1;
    for (;;) {
      const resposta = await this.listarClientes(pagina++);
      clientes.push(...resposta.items);
      if (!resposta.items.length || resposta.page * resposta.pageSize >= resposta.total) return clientes;
    }
  }

  async consultarCliente(id: string): Promise<Recurso<ClienteDetalhe>> {
    return recurso(await firstValueFrom(this.http.get<ClienteDetalhe>(`${this.base}/clientes/${id}`, { observe: 'response' })));
  }

  async salvarCliente(dados: DadosCliente, atual: Recurso<ClienteDetalhe> | null): Promise<void> {
    if (atual) {
      await firstValueFrom(this.http.put(`${this.base}/clientes/${atual.dados.id}`, dados, {
        headers: { 'If-Match': atual.etag },
      }));
    } else {
      await firstValueFrom(this.http.post(`${this.base}/clientes`, dados));
    }
  }

  async excluirCliente(id: string): Promise<void> {
    const atual = await this.consultarCliente(id);
    await firstValueFrom(this.http.delete(`${this.base}/clientes/${id}`, { headers: { 'If-Match': atual.etag } }));
  }

  async consultarCondutor(id: string): Promise<Recurso<Condutor>> {
    return recurso(await firstValueFrom(this.http.get<Condutor>(`${this.base}/condutores/${id}`, { observe: 'response' })));
  }

  async salvarCondutor(dados: DadosCondutor, atual: Recurso<Condutor> | null): Promise<void> {
    if (atual) {
      await firstValueFrom(this.http.put(`${this.base}/condutores/${atual.dados.id}`, dados, {
        headers: { 'If-Match': atual.etag },
      }));
    } else {
      await firstValueFrom(this.http.post(`${this.base}/condutores`, dados));
    }
  }

  async excluirCondutor(id: string): Promise<void> {
    const atual = await this.consultarCondutor(id);
    await firstValueFrom(this.http.delete(`${this.base}/condutores/${id}`, { headers: { 'If-Match': atual.etag } }));
  }

  listarVinculos(clienteId: string): Promise<Vinculo[]> {
    return firstValueFrom(this.http.get<Vinculo[]>(`${this.base}/clientes/${clienteId}/vinculos-pj`));
  }

  async consultarVinculo(clienteId: string, vinculoId: string): Promise<Recurso<Vinculo>> {
    return recurso(await firstValueFrom(this.http.get<Vinculo>(
      `${this.base}/clientes/${clienteId}/vinculos-pj/${vinculoId}`, { observe: 'response' },
    )));
  }

  async vincular(clienteId: string, outroClienteId: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base}/clientes/${clienteId}/vinculos-pj`, { outroClienteId }));
  }

  async desvincular(clienteId: string, vinculoId: string): Promise<void> {
    const atual = await this.consultarVinculo(clienteId, vinculoId);
    await firstValueFrom(this.http.delete(`${this.base}/clientes/${clienteId}/vinculos-pj/${vinculoId}`, {
      headers: { 'If-Match': atual.etag },
    }));
  }
}

function recurso<T>(resposta: HttpResponse<T>): Recurso<T> {
  const etag = resposta.headers.get('ETag');
  if (!resposta.body || !etag) throw new Error('A API não retornou os dados e a versão esperados.');
  return { dados: resposta.body, etag };
}
