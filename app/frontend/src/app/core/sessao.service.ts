import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  MENSAGEM_CADASTRO_CONFLITO,
  MENSAGEM_CADASTRO_LIMITE,
  MENSAGEM_CREDENCIAL,
  MENSAGEM_LIMITE,
  MENSAGEM_REVOGACAO,
  URL_API,
} from './configuracao';
import { MemoriaSessao } from './memoria-sessao';

interface SessaoRemota {
  accessToken: string;
  refreshToken: string;
  contaId: string;
  empresaId: string;
}

interface SessaoMinima {
  contaId: string;
  empresaId: string;
}

export interface ErroCampoServidor {
  campo: 'username' | 'email' | 'password';
  mensagem: string;
}

@Injectable({ providedIn: 'root' })
export class SessaoService {
  private readonly http = inject(HttpClient);
  readonly memoria = inject(MemoriaSessao);

  async registrar(
    username: string,
    email: string,
    password: string,
  ): Promise<ErroCampoServidor[] | null> {
    this.memoria.erro.set(null);
    try {
      await firstValueFrom(
        this.http.post(`${URL_API}/api/v1/registro`, { username, email, password }),
      );
      this.memoria.aviso.set('Conta criada. Entre com seu usuário e senha.');
      return null;
    } catch (erro) {
      if (erro instanceof HttpErrorResponse && erro.status === 400) {
        return this.errosDeCampo(erro);
      }

      const mensagem =
        erro instanceof HttpErrorResponse && erro.status === 409
          ? MENSAGEM_CADASTRO_CONFLITO
          : MENSAGEM_CADASTRO_LIMITE;
      this.memoria.erro.set(mensagem);
      return null;
    }
  }

  async entrar(username: string, password: string): Promise<string | null> {
    this.memoria.erro.set(null);
    this.memoria.revogacaoNaoConfirmada.set(false);
    try {
      const resposta = await firstValueFrom(
        this.http.post<SessaoRemota>(`${URL_API}/api/v1/session/login`, { username, password }),
      );
      this.memoria.accessToken.set(resposta.accessToken);
      this.memoria.refreshToken.set(resposta.refreshToken);
      this.memoria.contaId.set(resposta.contaId);
      this.memoria.empresaId.set(resposta.empresaId);
      this.memoria.aviso.set(null);
      return null;
    } catch (erro) {
      this.memoria.limparCredenciais();
      const mensagem =
        erro instanceof HttpErrorResponse && erro.status === 429 ? MENSAGEM_LIMITE : MENSAGEM_CREDENCIAL;
      this.memoria.erro.set(mensagem);
      return mensagem;
    }
  }

  async consultar(): Promise<void> {
    const resposta = await firstValueFrom(
      this.http.get<SessaoMinima>(`${URL_API}/api/v1/session`),
    );
    this.memoria.contaId.set(resposta.contaId);
    this.memoria.empresaId.set(resposta.empresaId);
  }

  async sair(): Promise<void> {
    try {
      await firstValueFrom(this.http.post(`${URL_API}/api/v1/session/logout`, {}));
      this.memoria.revogacaoNaoConfirmada.set(false);
      this.memoria.erro.set(null);
    } catch {
      this.memoria.revogacaoNaoConfirmada.set(true);
      this.memoria.erro.set(MENSAGEM_REVOGACAO);
    } finally {
      this.memoria.limparCredenciais();
    }
  }

  private errosDeCampo(erro: HttpErrorResponse): ErroCampoServidor[] {
    const mapa = (erro.error as { errors?: Record<string, string[]> } | null)?.errors ?? {};
    const campos = ['username', 'email', 'password'] as const;
    return campos.flatMap((campo) =>
      (mapa[campo] ?? []).map((mensagem) => ({ campo, mensagem })),
    );
  }
}
