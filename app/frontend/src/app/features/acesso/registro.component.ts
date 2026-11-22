import { Component, inject, signal } from '@angular/core';
import {
  email,
  form,
  FormField,
  FormRoot,
  maxLength,
  minLength,
  pattern,
  required,
} from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { LucideBuilding } from '@lucide/angular';
import { SessaoService } from '../../core/sessao.service';
import { ResumoErroComponent } from '../../shared/resumo-erro.component';
import { focarResumo, mensagemCampo } from './acesso-formulario';

@Component({
  selector: 'app-registro',
  imports: [FormField, FormRoot, RouterLink, LucideBuilding, ResumoErroComponent],
  templateUrl: './registro.component.html',
})
export class RegistroComponent {
  private readonly sessao = inject(SessaoService);
  private readonly roteador = inject(Router);
  protected readonly memoria = this.sessao.memoria;
  protected readonly mensagemCampo = mensagemCampo;
  protected readonly modelo = signal({ username: '', email: '', password: '' });
  protected readonly erroServidor = signal<{ username?: string; email?: string; password?: string }>({});

  private async enviar(): Promise<void> {
    this.erroServidor.set({});
    const atual = this.modelo();
    const erros = await this.sessao.registrar(atual.username, atual.email, atual.password);
    if (this.memoria.erro()) {
      await focarResumo();
      return;
    }

    if (erros && erros.length > 0) {
      const mapa: { username?: string; email?: string; password?: string } = {};
      for (const erro of erros) {
        mapa[erro.campo] = erro.mensagem;
      }
      this.erroServidor.set(mapa);
      return;
    }

    await this.roteador.navigateByUrl('/entrar');
  }

  protected readonly formulario = form(
    this.modelo,
    (caminho) => {
      required(caminho.username, { message: 'Informe o usuário.' });
      pattern(caminho.username, /^\S+$/u, { message: 'O usuário não pode conter espaços.' });
      maxLength(caminho.username, 256, { message: 'O usuário pode ter no máximo 256 caracteres.' });
      required(caminho.email, { message: 'Informe o e-mail.' });
      email(caminho.email, { message: 'Informe um e-mail válido.' });
      maxLength(caminho.email, 256, { message: 'O e-mail pode ter no máximo 256 caracteres.' });
      required(caminho.password, { message: 'Informe a senha.' });
      minLength(caminho.password, 8, { message: 'A senha precisa ter pelo menos 8 caracteres.' });
      maxLength(caminho.password, 128, { message: 'A senha pode ter no máximo 128 caracteres.' });
    },
    {
      submission: {
        action: async () => this.enviar(),
      },
    },
  );
}
